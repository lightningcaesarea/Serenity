using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Content.Server.Administration;
using Content.Server.Database;
using Content.Server.Discord.DiscordLink;
using Content.Shared._Serenity.CCVar;
using NetCord;
using NetCord.Rest;
using Robust.Server.Player;
using Robust.Shared.Asynchronous;
using Robust.Shared.Configuration;
using Robust.Shared.Network;
using DiscordInteraction = NetCord.Interaction;

namespace Content.Server._Serenity.DiscordLinking;

/// <summary>
/// Ties each SS14 account to exactly one Discord account and keeps the game behind guild membership.
/// A player without a link is refused at connect and shown a one-time code; they press the
/// "Link account" button in Discord and type the code into a private modal. Leaving or being
/// banned from the guild locks the account out, and kicks it if it's online.
/// </summary>
public sealed partial class DiscordAccountLinkManager : IPostInjectInit
{
    [Dependency] private DiscordLink _discord = default!;
    [Dependency] private IServerDbManager _db = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IPlayerManager _players = default!;
    [Dependency] private IPlayerLocator _locator = default!;
    [Dependency] private ITaskManager _tasks = default!;
    [Dependency] private ILogManager _log = default!;

    public const string PanelButtonId = "serenity-link-open";
    private const string ModalId = "serenity-link-submit";
    private const string CodeInputId = "serenity-link-code";

    // No 0/O, 1/I/L: the code is read off a disconnect screen and typed by hand.
    private const string CodeAlphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";
    private const int CodeLength = 6;
    private const int MaxFailedAttempts = 5;
    private static readonly TimeSpan FailedAttemptWindow = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan DiscordTimeout = TimeSpan.FromSeconds(5);

    private ISawmill _sawmill = default!;

    private readonly object _lock = new();
    private readonly Dictionary<string, PendingCode> _codes = new();
    private readonly Dictionary<NetUserId, string> _codeByPlayer = new();
    private readonly Dictionary<ulong, List<DateTime>> _failedAttempts = new();

    private bool _required;
    private string _invite = string.Empty;
    private int _codeMinutes;
    private ulong _staffRole;
    private ulong _logChannel;
    private bool _failOpen;

    private sealed record PendingCode(NetUserId UserId, string UserName, DateTime Expires);

    void IPostInjectInit.PostInject()
    {
        _sawmill = _log.GetSawmill("serenity.discord_link");
    }

    public void Initialize()
    {
        _cfg.OnValueChanged(SerenityCCVars.DiscordLinkRequired, v => _required = v, true);
        _cfg.OnValueChanged(SerenityCCVars.DiscordLinkInvite, v => _invite = v, true);
        _cfg.OnValueChanged(SerenityCCVars.DiscordLinkCodeMinutes, v => _codeMinutes = Math.Max(1, v), true);
        _cfg.OnValueChanged(SerenityCCVars.DiscordLinkStaffRole, v => _staffRole = ParseId(v), true);
        _cfg.OnValueChanged(SerenityCCVars.DiscordLinkLogChannel, v => _logChannel = ParseId(v), true);
        _cfg.OnValueChanged(SerenityCCVars.DiscordLinkFailOpen, v => _failOpen = v, true);

        _discord.OnInteractionReceived += OnInteraction;
        _discord.OnGuildUserRemoved += OnGuildUserRemoved;
        _discord.RegisterCommandCallback(OnLinkPanelCommand, "linkpanel");
        _discord.RegisterCommandCallback(OnWhoisCommand, "whois");
        _discord.RegisterCommandCallback(OnUnlinkCommand, "unlink");

        if (_required && !_discord.IsConnected)
            _sawmill.Error("serenity.discord_link.required is on but the Discord bot isn't configured; new players will be refused with codes they can't use.");
    }

    public void Shutdown()
    {
        _discord.OnInteractionReceived -= OnInteraction;
        _discord.OnGuildUserRemoved -= OnGuildUserRemoved;
    }

    private static ulong ParseId(string value)
        => ulong.TryParse(value.Trim(), out var id) ? id : 0;

    #region Connection gate

    /// <summary>
    /// Returns a deny message, or null to let the player in. Called for non-admins only.
    /// </summary>
    public async Task<string?> CheckConnection(NetUserId userId, string userName)
    {
        if (!_required)
            return null;

        var link = await _db.GetDiscordLinkByPlayer(userId.UserId);
        if (link == null)
            return UnlinkedMessage(GetOrCreateCode(userId, userName));

        var discordId = unchecked((ulong) link.DiscordId);

        GuildUser? member;
        try
        {
            using var cts = new CancellationTokenSource(DiscordTimeout);
            member = await _discord.GetGuildUserAsync(discordId, cts.Token);
        }
        catch (Exception e)
        {
            _sawmill.Warning($"Couldn't check Discord membership for {userName} ({discordId}): {e.Message}");
            return _failOpen ? null : Loc.GetString("serenity-discord-link-deny-unavailable");
        }

        if (member == null)
        {
            bool? banned;
            using (var cts = new CancellationTokenSource(DiscordTimeout))
            {
                try
                {
                    banned = await _discord.IsBannedAsync(discordId, cts.Token);
                }
                catch (Exception)
                {
                    banned = null;
                }
            }

            return banned == true
                ? Loc.GetString("serenity-discord-link-deny-banned")
                : Loc.GetString("serenity-discord-link-deny-not-member", ("invite", InviteText()));
        }

        if (member.Username != link.DiscordUsername)
            await _db.UpdateDiscordUsername(userId.UserId, member.Username);

        return null;
    }

    private string UnlinkedMessage(string code)
    {
        return Loc.GetString("serenity-discord-link-deny-unlinked",
            ("invite", InviteText()),
            ("code", code),
            ("minutes", _codeMinutes));
    }

    private string InviteText()
        => string.IsNullOrWhiteSpace(_invite) ? Loc.GetString("serenity-discord-link-no-invite") : _invite;

    private string GetOrCreateCode(NetUserId userId, string userName)
    {
        lock (_lock)
        {
            PruneExpired();

            // Reconnecting shouldn't invalidate a code the player is halfway through typing.
            if (_codeByPlayer.TryGetValue(userId, out var existing))
                return existing;

            string code;
            do
            {
                code = RandomNumberGenerator.GetString(CodeAlphabet, CodeLength);
            } while (_codes.ContainsKey(code));

            _codes[code] = new PendingCode(userId, userName, DateTime.UtcNow.AddMinutes(_codeMinutes));
            _codeByPlayer[userId] = code;
            return code;
        }
    }

    private void PruneExpired()
    {
        var now = DateTime.UtcNow;
        foreach (var (code, pending) in _codes.Where(p => p.Value.Expires <= now).ToList())
        {
            _codes.Remove(code);
            _codeByPlayer.Remove(pending.UserId);
        }
    }

    #endregion

    #region Discord interactions

    private async ValueTask OnInteraction(DiscordInteraction interaction)
    {
        switch (interaction)
        {
            case ButtonInteraction button when button.Data.CustomId == PanelButtonId:
                await button.SendResponseAsync(InteractionCallback.Modal(BuildModal()));
                break;
            case ModalInteraction modal when modal.Data.CustomId == ModalId:
                await HandleModalSubmit(modal);
                break;
        }
    }

    private static ModalProperties BuildModal()
    {
        return new ModalProperties(ModalId, Loc.GetString("serenity-discord-link-modal-title"),
        [
            new TextInputProperties(CodeInputId, TextInputStyle.Short, Loc.GetString("serenity-discord-link-modal-label"))
            {
                MinLength = CodeLength,
                MaxLength = CodeLength,
                Placeholder = "K7Q2XP",
            },
        ]);
    }

    private async Task HandleModalSubmit(ModalInteraction modal)
    {
        var user = modal.User;
        var typed = ReadCode(modal)?.Trim().ToUpperInvariant() ?? string.Empty;

        PendingCode? pending;
        lock (_lock)
        {
            if (IsRateLimited(user.Id))
                pending = null;
            else if (_codes.TryGetValue(typed, out pending) && pending.Expires <= DateTime.UtcNow)
                pending = null;

            if (pending == null)
                RecordFailure(user.Id);
        }

        if (pending == null)
        {
            await ReplyPrivately(modal, Loc.GetString("serenity-discord-link-reply-bad-code"));
            return;
        }

        // Discord drops interactions not answered within 3 s, so only local DB lookups on this path.
        if (await _db.GetDiscordLinkByDiscord(user.Id) is { } existing)
        {
            var other = await _db.GetPlayerRecordByUserId(new NetUserId(existing.PlayerUserId));
            await ReplyPrivately(modal, Loc.GetString("serenity-discord-link-reply-discord-taken",
                ("player", other?.LastSeenUserName ?? existing.PlayerUserId.ToString())));
            return;
        }

        if (!await _db.AddDiscordLink(pending.UserId.UserId, user.Id, user.Username))
        {
            await ReplyPrivately(modal, Loc.GetString("serenity-discord-link-reply-already-linked"));
            return;
        }

        lock (_lock)
        {
            _codes.Remove(typed);
            _codeByPlayer.Remove(pending.UserId);
        }

        _sawmill.Info($"Linked {pending.UserName} ({pending.UserId}) to Discord {user.Username} ({user.Id})");
        await ReplyPrivately(modal, Loc.GetString("serenity-discord-link-reply-success", ("player", pending.UserName)));
        await PostLog(Loc.GetString("serenity-discord-link-log-linked",
            ("discordId", user.Id.ToString()),
            ("player", pending.UserName),
            ("userId", pending.UserId.ToString())));
    }

    private static string? ReadCode(ModalInteraction modal)
    {
        foreach (var component in modal.Data.Components)
        {
            if (component is TextInput { CustomId: CodeInputId } input)
                return input.Value;
        }

        return null;
    }

    private bool IsRateLimited(ulong discordId)
    {
        if (!_failedAttempts.TryGetValue(discordId, out var attempts))
            return false;

        var cutoff = DateTime.UtcNow - FailedAttemptWindow;
        attempts.RemoveAll(t => t < cutoff);
        return attempts.Count >= MaxFailedAttempts;
    }

    private void RecordFailure(ulong discordId)
    {
        if (!_failedAttempts.TryGetValue(discordId, out var attempts))
            _failedAttempts[discordId] = attempts = new List<DateTime>();

        attempts.Add(DateTime.UtcNow);
    }

    private static async Task ReplyPrivately(DiscordInteraction interaction, string content)
    {
        await interaction.SendResponseAsync(InteractionCallback.Message(new InteractionMessageProperties
        {
            Content = content,
            Flags = MessageFlags.Ephemeral,
            AllowedMentions = AllowedMentionsProperties.None,
        }));
    }

    private async void OnGuildUserRemoved(ulong discordId)
    {
        try
        {
            var link = await _db.GetDiscordLinkByDiscord(discordId);
            if (link == null)
                return;

            var userId = new NetUserId(link.PlayerUserId);
            _tasks.RunOnMainThread(() =>
            {
                if (!_required || !_players.TryGetSessionById(userId, out var session))
                    return;

                session.Channel.Disconnect(Loc.GetString("serenity-discord-link-kick-removed"));
            });

            var located = await _locator.LookupIdAsync(userId);
            await PostLog(Loc.GetString("serenity-discord-link-log-removed",
                ("discordId", discordId.ToString()),
                ("player", located?.Username ?? link.PlayerUserId.ToString())));
        }
        catch (Exception e)
        {
            _sawmill.Error($"Error handling Discord member removal for {discordId}: {e}");
        }
    }

    #endregion

    #region Discord staff commands

    private async void OnLinkPanelCommand(CommandReceivedEventArgs args)
    {
        try
        {
            if (!await IsStaff(args))
                return;

            await _discord.SendMessageAsync(args.Message.ChannelId, new MessageProperties
            {
                Content = Loc.GetString("serenity-discord-link-panel-text"),
                Components =
                [
                    new ActionRowProperties(
                    [
                        new ButtonProperties(PanelButtonId, Loc.GetString("serenity-discord-link-panel-button"), ButtonStyle.Primary),
                    ]),
                ],
            });
        }
        catch (Exception e)
        {
            _sawmill.Error($"Failed to post link panel: {e}");
        }
    }

    private async void OnWhoisCommand(CommandReceivedEventArgs args)
    {
        try
        {
            if (!await IsStaff(args))
                return;

            if (args.Arguments.Count != 1)
            {
                await Reply(args, Loc.GetString("serenity-discord-link-whois-usage", ("prefix", _discord.BotPrefix)));
                return;
            }

            var link = await ResolveLink(args.Arguments[0]);
            await Reply(args, link == null
                ? Loc.GetString("serenity-discord-link-whois-none")
                : await DescribeLink(link));
        }
        catch (Exception e)
        {
            _sawmill.Error($"whois failed: {e}");
        }
    }

    private async void OnUnlinkCommand(CommandReceivedEventArgs args)
    {
        try
        {
            if (!await IsStaff(args))
                return;

            if (args.Arguments.Count != 1)
            {
                await Reply(args, Loc.GetString("serenity-discord-link-unlink-usage", ("prefix", _discord.BotPrefix)));
                return;
            }

            var link = await ResolveLink(args.Arguments[0]);
            if (link == null || !await _db.RemoveDiscordLink(link.PlayerUserId))
            {
                await Reply(args, Loc.GetString("serenity-discord-link-whois-none"));
                return;
            }

            var description = await DescribeLink(link);
            await Reply(args, Loc.GetString("serenity-discord-link-unlinked", ("link", description)));
            await PostLog(Loc.GetString("serenity-discord-link-log-unlinked",
                ("link", description),
                ("by", args.Message.Author.Username)));
        }
        catch (Exception e)
        {
            _sawmill.Error($"unlink failed: {e}");
        }
    }

    private async Task<bool> IsStaff(CommandReceivedEventArgs args)
    {
        if (_staffRole == 0 || args.Message.GuildId == null)
            return false;

        var member = await _discord.GetGuildUserAsync(args.Message.Author.Id);
        return member != null && member.RoleIds.Contains(_staffRole);
    }

    private static async Task Reply(CommandReceivedEventArgs args, string content)
    {
        await args.Message.ReplyAsync(new ReplyMessageProperties
        {
            Content = content,
            AllowedMentions = AllowedMentionsProperties.None,
        });
    }

    /// <summary>
    /// Accepts a Discord mention, a Discord ID, or an SS14 username / user ID.
    /// </summary>
    private async Task<SerenityDiscordLink?> ResolveLink(string query)
    {
        var trimmed = query.Trim().TrimStart('<').TrimEnd('>').TrimStart('@', '!');
        if (ulong.TryParse(trimmed, out var discordId))
            return await _db.GetDiscordLinkByDiscord(discordId);

        var located = await _locator.LookupIdByNameOrIdAsync(query);
        return located == null ? null : await _db.GetDiscordLinkByPlayer(located.UserId.UserId);
    }

    #endregion

    #region Shared helpers

    /// <summary>
    /// One-line summary used by both the bot and the in-game admin commands.
    /// </summary>
    public async Task<string> DescribeLink(SerenityDiscordLink link)
    {
        var located = await _locator.LookupIdAsync(new NetUserId(link.PlayerUserId));
        return Loc.GetString("serenity-discord-link-describe",
            ("player", located?.Username ?? "?"),
            ("userId", link.PlayerUserId.ToString()),
            ("discordId", unchecked((ulong) link.DiscordId).ToString()),
            ("discordName", link.DiscordUsername ?? "?"),
            ("linkedAt", link.LinkedAt.ToString("yyyy-MM-dd HH:mm")));
    }

    public async Task PostLog(string message)
    {
        if (_logChannel == 0)
            return;

        try
        {
            await _discord.SendMessageAsync(_logChannel, new MessageProperties
            {
                Content = message,
                AllowedMentions = AllowedMentionsProperties.None,
            });
        }
        catch (Exception e)
        {
            _sawmill.Warning($"Couldn't post to the link log channel: {e.Message}");
        }
    }

    #endregion
}
