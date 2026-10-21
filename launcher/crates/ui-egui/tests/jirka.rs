use artcraft_launcher_ui_egui::jirka::{Entry, Event, JirkaUi, Preferences, StoreApp};
use egui_kittest::{Harness, kittest::Queryable};

fn entries() -> Vec<Entry> {
    let value: serde_json::Value = serde_json::from_str(include_str!("../../../../catalog/catalog.json")).unwrap();
    let apps: Vec<StoreApp> = serde_json::from_value(value["apps"].clone()).unwrap();
    apps.into_iter()
        .map(|app| Entry {
            state: match app.kind.as_str() {
                "draft" => "draft",
                "apk" => "android",
                "portable-zip" => "available",
                _ => "external",
            }
            .into(),
            app,
            ..Entry::default()
        })
        .collect()
}
fn harness(width: f32, height: f32, scale: f32, dark: bool, table: bool, data: Vec<Entry>) -> Harness<'static, Option<JirkaUi>> {
    Harness::builder().with_size(egui::vec2(width / scale, height / scale)).with_pixels_per_point(scale).build_ui_state(
        move |ui, state: &mut Option<JirkaUi>| {
            if state.is_none() {
                let mut launcher = JirkaUi::new(ui.ctx(), Preferences { dark, table, scale });
                launcher.accept(Event {
                    r#type: "snapshot".into(),
                    apps: data.clone(),
                    message: "Katalog připraven · lokální kontrola".into(),
                    ..Event::default()
                });
                *state = Some(launcher);
            }
            if let Some(launcher) = state {
                launcher.ui(ui, true);
            }
        },
        None,
    )
}
#[test]
fn cards_resize_without_clipping_long_text() {
    let mut data = entries();
    data.push(Entry {
        app: StoreApp {
            id: "long-name".into(),
            name: "Aplikace s velmi dlouhým názvem pro ověření zalamování a správné výšky karty při změně rozměru okna".into(),
            description: "Dlouhý popis aplikace musí zůstat celý čitelný. ".repeat(8),
            category: "pc".into(),
            version: "123.456.789.123".into(),
            ..StoreApp::default()
        },
        state: "installed".into(),
        ..Entry::default()
    });
    for (width, height, scale) in
        [(1390., 900., 1.), (1000., 700., 1.), (900., 600., 1.), (800., 600., 1.), (520., 360., 1.), (800., 600., 1.5), (520., 500., 1.5)]
    {
        for dark in [true, false] {
            let mut h = harness(width, height, scale, dark, false, data.clone());
            h.run_steps(5);
            for label in [
                "Mřížka",
                "Tabulka",
                "Nastavení",
                "Close",
                "Vše 12",
                "Nainstalované 1",
                "Aktualizace 0",
                "Nenainstalované 11",
                "Home Assistant OS",
                "Ostatní",
            ] {
                let rect = h.get_by_role_and_label(egui::accesskit::Role::Button, label).rect();
                assert!(rect.right() <= width / scale + 1.0, "{label} exceeds window at {width}/{scale}: {rect:?}");
            }
            let state = h.state().as_ref().unwrap();
            assert!(!state.card_bounds.is_empty());
            for (id, inner, text) in &state.card_bounds {
                assert!(inner.right() <= width / scale, "{width}/{scale} {id} card outside viewport: {inner:?}");
                assert!(text.right() <= inner.right() + 1.0, "{width}/{scale} {id} text too wide: {text:?}, {inner:?}");
                assert!(text.bottom() <= inner.bottom() - 34.0, "{width}/{scale} {id} overlaps actions: {text:?}, {inner:?}");
            }
            for (id, inner, action) in &state.action_bounds {
                assert!(action.right() <= inner.right() + 1.0, "{width}/{scale} {id}: actions overflow {action:?} {inner:?}");
            }
            if let Some(dir) = std::env::var_os("JIRKA_SNAPSHOTS") {
                h.render().unwrap().save(std::path::Path::new(&dir).join(format!("grid-{width}-{height}-{scale}-{dark}.png"))).unwrap();
            }
        }
    }
}
#[test]
fn table_and_filters_and_install_confirmation() {
    let mut data = entries();
    let entry = data.first_mut().unwrap();
    entry.app.kind = "portable-zip".into();
    entry.state = "available".into();
    let mut h = harness(1180., 900., 1., true, true, vec![entry.clone()]);
    h.run_steps(5);
    h.get_by_label("Instalovat").click();
    h.run_steps(3);
    assert!(h.state_mut().as_mut().unwrap().drain_commands().is_empty(), "Install must be confirmed first");
    h.get_by_label("Zpět").click();
    h.run_steps(3);
    assert!(h.state_mut().as_mut().unwrap().drain_commands().is_empty());
    let state = h.state_mut().as_mut().unwrap();
    state.search = "nenalezeno".into();
    assert!(state.visible_entries().is_empty());
    state.search.clear();
    state.category = "esp32".into();
    assert!(state.visible_entries().is_empty());
    state.category.clear();
    state.filter = 1;
    assert!(state.visible_entries().is_empty());
    state.filter = 0;
    h.run_steps(3);
    if let Some(dir) = std::env::var_os("JIRKA_SNAPSHOTS") {
        h.render().unwrap().save(std::path::Path::new(&dir).join("table-dark.png")).unwrap();
    }
}

#[test]
fn catalogue_previews() {
    let Some(dir) = std::env::var_os("JIRKA_SNAPSHOTS") else { return };
    for (dark, table, name) in [(true, false, "jirka-store-dark"), (false, false, "jirka-store-light"), (true, true, "jirka-store-table")] {
        let mut h = harness(1390., 900., 1., dark, table, entries());
        h.run_steps(5);
        h.render().unwrap().save(std::path::Path::new(&dir).join(format!("{name}.png"))).unwrap();
    }
}
