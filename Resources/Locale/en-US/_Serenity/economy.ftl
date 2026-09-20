## Currency exchange
currency-exchange-success = Exchanged {$input} for {$output}.
currency-exchange-too-small = That is too little to exchange.

## Balance admin commands
cmd-balance-desc = Show a player's Sector Credit balance and their five most recent ledger entries.
cmd-balance-help = Usage: balance <name or user id>
cmd-balance_ledger-desc = Show a player's Sector Credit ledger, newest first.
cmd-balance_ledger-help = Usage: balance_ledger <name or user id> [count, default 20, max 500]
cmd-balance_adjust-desc = Add or remove Sector Credits from a player, online or offline. Logged with the reason.
cmd-balance_adjust-help = Usage: balance_adjust <name or user id> <delta> <reason...>
cmd-balance_set-desc = Set a player's Sector Credits to an exact value, online or offline. Logged with the reason.
cmd-balance_set-help = Usage: balance_set <name or user id> <value> <reason...>

cmd-balance-player-not-found = No player found matching "{$player}".
cmd-balance-bad-amount = Amount must be a non-zero number (non-negative for set).
cmd-balance-bad-count = Count must be a whole number between 1 and 500.
cmd-balance-header = {$player} ({$state}): {$balance} Sector Credits. Recent ledger:
cmd-balance-ledger-header = Ledger for {$player} — {$count} most recent:
cmd-balance-ledger-empty = {$player} has no ledger entries.
cmd-balance-adjusted = Adjusted {$player} by {$delta}; balance is now {$balance}.
cmd-balance-set = Set {$player} from {$before} to {$balance}.
