alerts-pain-name = { $severity ->
    [0] Aching
    [1] In Pain
    [2] Severe Pain
    *[3] Agony
}
alerts-pain-desc = { $severity ->
    [0] Everything aches. It is [color=yellow]bearable[/color] for now.
    [1] You are in [color=yellow]real pain[/color] and moving is harder. Painkillers will [color=green]dull it[/color], but they will not fix what is wrong.
    [2] The pain is [color=red]severe[/color]. You are [color=yellow]slowed[/color] and may [color=yellow]drop what you are holding[/color] if you are hit. Find [color=green]medical[/color].
    *[3] You are in [color=red]agony[/color]. You can barely function and will [color=yellow]often drop things[/color] when hit. Get [color=green]medical help[/color] now.
}

pain-rise-mild = Your injuries ache.
pain-rise-moderate = Pain flares through your body.
pain-rise-severe = Pain grips you. It is hard to stay focused.
pain-rise-agonizing = Agony tears through you!
pain-easing = The pain eases a little.
pain-gone = The pain fades away.
pain-numbed = A numbness spreads through you and the pain fades. It will not last.

pain-examine-moderate = [color=yellow]{ CAPITALIZE(SUBJECT($target)) } winces with every movement.[/color]
pain-examine-severe = [color=orange]{ CAPITALIZE(SUBJECT($target)) } is clearly in serious pain.[/color]
pain-examine-agonizing = [color=red]{ CAPITALIZE(SUBJECT($target)) } is in agony.[/color]
pain-examine-masked = [color=lightblue]{ CAPITALIZE(SUBJECT($target)) } looks numb, as if drugged against pain.[/color]

reagent-name-analgesin = analgesin
reagent-desc-analgesin = A mild, long-lasting painkiller. Dulls minor pain but does nothing for the injury itself. Harmful in large doses.

reagent-name-antiflam = antiflam
reagent-desc-antiflam = An anti-inflammatory that eases the pain of broken bones and burns. Thins the blood, so open wounds bleed more while it lasts.

reagent-name-dolorphine = dolorphine
reagent-desc-dolorphine = A powerful opioid painkiller for serious trauma. Causes drowsiness, and an overdose slows breathing dangerously.

reagent-name-lidonol = lidonol
reagent-desc-lidonol = A local numbing agent. Deadens the pain of burns and cuts but does nothing for broken bones.

reagent-name-somnacaine = somnacaine
reagent-desc-somnacaine = A surgical anesthetic. Blocks all pain and puts the patient to sleep. Overdoses suppress breathing.
