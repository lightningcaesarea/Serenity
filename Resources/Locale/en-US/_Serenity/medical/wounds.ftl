# Fractures
wound-bluntfracture-1 = Hairline Fracture
wound-bluntfracture-2 = Compound Fracture
wound-bluntfracture-3 = Shattered Bone

# Heat Burns
wound-heatburn-1 = Moderate Burn
wound-heatburn-2 = Severe Burn
wound-heatburn-3 = Catastrophic Burn

# Cold Burns
wound-coldburn-1 = Mild Frostbite
wound-coldburn-2 = Severe Frostbite
wound-coldburn-3 = Deep Frostbite

# Shock Burns
wound-shockburn-1 = Minor Shock Burn
wound-shockburn-2 = Severe Shock Burn
wound-shockburn-3 = Catastrophic Shock Burn

# Caustic Burns
wound-causticburn-1 = Minor Chemical Burn
wound-causticburn-2 = Severe Chemical Burn
wound-causticburn-3 = Catastrophic Chemical Burn

# Radiation Burns
wound-radiationburn-1 = Minor Radiation Burn
wound-radiationburn-2 = Severe Radiation Burn
wound-radiationburn-3 = Catastrophic Radiation Burn

# Bleeding
wound-bleed-slash-1 = Minor Bleeding
wound-bleed-slash-2 = Heavy Bleeding
wound-bleed-slash-3 = Hemorrhaging

wound-bleed-piercing-1 = Minor Bleeding
wound-bleed-piercing-2 = Heavy Bleeding
wound-bleed-piercing-3 = Hemorrhaging

# Examine text
wound-examine-entry = {$name} (Tier {$tier})
wound-examine-header = [bold]Wounds:[/bold]

# Flavor lines pushed onto the health-verb examine for external observers.
# Self-Aware characters get a clinical readout via SelfAwareSystem instead.
wound-examine-fracture-1 = [color=#CED2D1]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-BE($target) } favoring an injured limb.[/color]
wound-examine-fracture-2 = [color=#FF8787]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-HAVE($target) } a clearly broken bone.[/color]
wound-examine-fracture-3 = [color=#FF3636]{ CAPITALIZE(POSS-ADJ($target)) } bones look horribly shattered.[/color]

wound-examine-burn-1 = [color=#FFA8A8]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-HAVE($target) } angry red burns.[/color]
wound-examine-burn-2 = [color=#FF6868]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-HAVE($target) } severe burns across { POSS-ADJ($target) } skin.[/color]
wound-examine-burn-3 = [color=#FF2121]{ CAPITALIZE(POSS-ADJ($target)) } flesh is charred and blistered.[/color]

# HUD Alerts
alerts-wound-fracture-name = { $severity ->
    [0] Hairline Fracture
    [1] Compound Fracture
    *[2] Shattered Bone
}
alerts-wound-fracture-desc = { $severity ->
    [0] Something cracked. Get to [color=green]medical[/color] before it gets worse.
    [1] You have a [color=red]compound fracture[/color]. Moving is painful and [color=yellow]slow[/color].
    *[2] Your bones are [color=red]shattered[/color]. You can barely move and might [color=yellow]drop whatever you're holding[/color] if you take another hit.
}
alerts-wound-burn-name = { $severity ->
    [0] Moderate Burn
    [1] Severe Burn
    *[2] Catastrophic Burn
}
alerts-wound-burn-desc = { $severity ->
    [0] That's going to blister. Get to [color=green]medical[/color] before it gets worse.
    [1] You have [color=red]severe burns[/color]. The pain is [color=yellow]slowing you down[/color].
    *[2] You're [color=red]badly burned[/color]. Every step is agony and your movement is [color=yellow]slowed[/color].
}
