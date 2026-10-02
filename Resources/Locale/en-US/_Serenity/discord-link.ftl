## Connection refusals

serenity-discord-link-deny-unlinked =
    This server requires your SS14 account to be linked to Discord.

    1. Join our Discord: {$invite}
    2. Find the verification channel and press "Link account".
    3. Enter this code: {$code}
    4. Reconnect.

    The code expires in {$minutes} minutes. Don't share it with anyone.
serenity-discord-link-deny-not-member =
    Your linked Discord account isn't in our Discord server.
    Rejoin to play: {$invite}
serenity-discord-link-deny-banned = Your linked Discord account is banned from our Discord server, so you can't join the game.
serenity-discord-link-deny-unavailable = We can't reach Discord to verify your account right now. Please try again in a few minutes.
serenity-discord-link-no-invite = ask a staff member for an invite
serenity-discord-link-kick-removed = Your linked Discord account left or was removed from our Discord server.

## Discord bot

serenity-discord-link-panel-text = **Link your SS14 account.** Try to connect to the game server first to get your code, then press the button below and enter it. Only you will see what you type.
serenity-discord-link-panel-button = Link account
serenity-discord-link-modal-title = Link your SS14 account
serenity-discord-link-modal-label = Code from the game's connection screen
serenity-discord-link-reply-success = Linked to SS14 account **{$player}**. You can connect now.
serenity-discord-link-reply-bad-code = That code isn't valid or has expired. Connect to the game again to get a fresh one.
serenity-discord-link-reply-discord-taken = This Discord account is already linked to SS14 account **{$player}**. Ask staff if you need that changed.
serenity-discord-link-reply-already-linked = That SS14 account is already linked. Ask staff if you need that changed.
serenity-discord-link-log-linked = Linked: Discord **{$discordName}** (`{$discordId}`) <-> SS14 **{$player}** (`{$userId}`)
serenity-discord-link-log-removed = Left or was removed from the Discord while linked: Discord **{$discordName}** (`{$discordId}`) <-> SS14 **{$player}** (`{$userId}`)
serenity-discord-link-log-unlinked = Unlinked by **{$by}** (`{$byId}`): {$link}
serenity-discord-link-whois-usage = Usage: {$prefix}whois <@user | Discord ID | SS14 username>
serenity-discord-link-unlink-usage = Usage: {$prefix}unlink <@user | Discord ID | SS14 username>
serenity-discord-link-whois-none = No link found.
serenity-discord-link-unlinked = Removed link: {$link}
serenity-discord-link-describe = SS14 {$player} ({$userId}) <-> Discord {$discordName} ({$discordId}), linked {$linkedAt} UTC

## Admin commands

cmd-discordlink_info-desc = Shows which Discord account a player is linked to.
cmd-discordlink_info-help = Usage: discordlink_info <username or user ID>
cmd-discordlink_lookup-desc = Finds the SS14 account linked to a Discord user ID.
cmd-discordlink_lookup-help = Usage: discordlink_lookup <Discord user ID>
cmd-discordlink_remove-desc = Removes a player's Discord link so they can link a different account.
cmd-discordlink_remove-help = Usage: discordlink_remove <username or user ID>
serenity-discord-link-cmd-no-player = Couldn't find a player called {$player}.
serenity-discord-link-cmd-not-linked = {$player} has no linked Discord account.
serenity-discord-link-cmd-discord-not-linked = Discord user {$discordId} isn't linked to any SS14 account.
serenity-discord-link-cmd-hint-player = <username or user ID>
