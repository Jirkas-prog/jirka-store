# Jirka Store — zdrojový fork ArtCraft Launcheru

Převzato z https://github.com/storytold/craft-launcher, commit
`6e7a122866591c067344687c45c92f735a1d189b` (lokální klon v D:/09_Craft).
Původní zdroje, fonty, licence a atribuce jsou zachovány.

`apps/jirka-launcher` sestavuje skutečný nativní Rust/egui launcher.
`crates/ui-egui/src/jirka.rs` upravuje původní header, card, grid a table z
`views.rs`; používá přímo původní `theme.rs`, `widgets.rs` a přibalené fonty.
Není to WinForms napodobenina. Původní logo se v Jirka Store nepoužívá.

Katalog a instalace zajišťuje `src/Store.Host` nad existujícím `Store.Core`.
Komunikace probíhá JSON zprávami přes soukromý stdin/stdout potomka, bez serveru.
Zůstávají kontroly SHA-256, cest v ZIP, verzí a atomické aktivace instalace.
Nové aplikace se načítají dynamicky z katalogu bez rekompilace launcheru.
