//! Jirka Store adaptation of views.rs (ArtCraft Team, MIT OR Apache-2.0).
//! Uses the original theme, fonts, square widgets, card strips and table builder.
//! Dynamic catalogue and actions are supplied by the existing validated Store.Host.

use crate::{
    theme::{self, ThemeChoice, Tokens},
    widgets::{self, Cell, Kind},
};
use egui::{Color32, FontId, Rect, RichText, Sense, Ui, vec2};
use serde::{Deserialize, Serialize};

pub const CATEGORIES: [(&str, &str); 6] = [
    ("pc", "PC · Windows"),
    ("esp32", "ESP32"),
    ("homeassistant", "Home Assistant OS"),
    ("android", "Android"),
    ("web", "Web"),
    ("other", "Ostatní"),
];
pub const DEFAULT_SOURCE: &str = "https://raw.githubusercontent.com/Jirkas-prog/jirka-store/main/catalog/catalog.json";

#[derive(Clone, Debug, Default, Deserialize)]
#[serde(default, rename_all = "camelCase")]
pub struct StoreApp {
    pub id: String,
    pub name: String,
    pub category: String,
    pub description: String,
    pub version: String,
    pub kind: String,
    pub repository: String,
}
#[derive(Clone, Debug, Default, Deserialize)]
#[serde(default, rename_all = "camelCase")]
pub struct Entry {
    pub app: StoreApp,
    pub state: String,
    pub installed_version: String,
}
#[derive(Debug, Default, Deserialize)]
#[serde(default, rename_all = "camelCase")]
pub struct Event {
    pub r#type: String,
    pub apps: Vec<Entry>,
    pub source: String,
    pub loaded_source: String,
    pub busy: bool,
    pub active_id: String,
    pub message: String,
}
#[derive(Clone, Debug, Serialize)]
pub struct Command {
    pub command: String,
    pub id: Option<String>,
    pub source: Option<String>,
}
#[derive(Clone, Serialize, Deserialize)]
#[serde(default)]
pub struct Preferences {
    pub dark: bool,
    pub table: bool,
    pub scale: f32,
}
impl Default for Preferences {
    fn default() -> Self {
        Self { dark: true, table: false, scale: 1.0 }
    }
}

pub struct JirkaUi {
    pub entries: Vec<Entry>,
    pub preferences: Preferences,
    pub busy: bool,
    pub message: String,
    pub source: String,
    pub loaded_source: String,
    pub active_id: String,
    pub filter: usize,
    pub category: String,
    pub search: String,
    pub settings_open: bool,
    pub error: Option<String>,
    pub card_bounds: Vec<(String, Rect, Rect)>,
    pub action_bounds: Vec<(String, Rect, Rect)>,
    commands: Vec<Command>,
    confirm: Option<Vec<String>>,
    detail: Option<Entry>,
    source_edit: String,
    alphabetical: bool,
    fonts_ready: bool,
}

impl JirkaUi {
    pub fn new(ctx: &egui::Context, preferences: Preferences) -> Self {
        theme::install_fonts(ctx, false);
        egui_extras::install_image_loaders(ctx);
        Self {
            entries: vec![],
            preferences,
            busy: false,
            message: "Načítám katalog…".into(),
            source: DEFAULT_SOURCE.into(),
            loaded_source: DEFAULT_SOURCE.into(),
            active_id: String::new(),
            filter: 0,
            category: String::new(),
            search: String::new(),
            settings_open: false,
            error: None,
            card_bounds: vec![],
            action_bounds: vec![],
            commands: vec![],
            confirm: None,
            detail: None,
            source_edit: DEFAULT_SOURCE.into(),
            alphabetical: false,
            fonts_ready: false,
        }
    }
    pub fn accept(&mut self, event: Event) {
        if event.r#type == "snapshot" {
            self.entries = event.apps;
            self.busy = event.busy;
            self.active_id = event.active_id;
            self.source = event.source;
            self.loaded_source = event.loaded_source;
            if !self.settings_open {
                self.source_edit = self.source.clone();
            }
        }
        if event.r#type == "error" {
            self.error = Some(event.message.clone());
        }
        self.message = event.message;
    }
    pub fn drain_commands(&mut self) -> Vec<Command> {
        std::mem::take(&mut self.commands)
    }
    fn send(&mut self, command: &str, id: Option<&str>) {
        self.commands.push(Command { command: command.into(), id: id.map(str::to_string), source: None });
    }
    fn installed(e: &Entry) -> bool {
        matches!(e.state.as_str(), "installed" | "update")
    }
    fn matches_filter(&self, e: &Entry, f: usize) -> bool {
        match f {
            1 => Self::installed(e),
            2 => e.state == "update",
            3 => !Self::installed(e),
            _ => true,
        }
    }
    pub fn visible_entries(&self) -> Vec<Entry> {
        let needle = self.search.trim().to_lowercase();
        let mut entries: Vec<_> = self
            .entries
            .iter()
            .filter(|e| {
                (self.category.is_empty() || self.category == e.app.category)
                    && self.matches_filter(e, self.filter)
                    && format!("{} {}", e.app.name, e.app.description).to_lowercase().contains(&needle)
            })
            .cloned()
            .collect();
        if self.alphabetical {
            entries.sort_by_key(|e| e.app.name.to_lowercase());
        }
        entries
    }
    pub fn ui(&mut self, ui: &mut Ui, custom_title: bool) {
        let ctx = ui.ctx().clone();
        if !self.fonts_ready {
            self.fonts_ready = true;
            ctx.request_repaint();
            return;
        }
        theme::apply(&ctx, if self.preferences.dark { ThemeChoice::Dark } else { ThemeChoice::Light });
        let t = Tokens::get(&ctx);
        if self.busy {
            ctx.request_repaint_after(std::time::Duration::from_millis(100));
        }
        let compact = ui.available_width() < 900.0;
        let full_branding = ui.available_width() >= 1100.0;
        egui::Panel::top("jirka-header").frame(egui::Frame::new().fill(t.bg)).exact_size(48.0).show(ui, |ui| {
            if custom_title {
                widgets::title_bar_drag(ui);
            }
            ui.horizontal(|ui| {
                ui.spacing_mut().item_spacing.x = 0.0;
                ui.add_space(if compact { 12.0 } else { 20.0 });
                let size = if ui.available_width() < 390.0 { 17.0 } else { 21.0 };
                ui.label(RichText::new("JIRKA STORE").font(FontId::new(size, theme::display())).color(t.ink_strong));
                ui.add_space(if compact { 12.0 } else { 20.0 });
                widgets::rule(ui, 48.0);
                if full_branding {
                    ui.add_space(16.0);
                    widgets::tag(ui, "Launcher", t.invert_bg, t.invert_fg);
                    ui.add_space(12.0);
                    widgets::hud(ui, "Vaše aplikace", t.muted);
                }
                ui.with_layout(egui::Layout::right_to_left(egui::Align::Center), |ui| {
                    if custom_title {
                        widgets::window_buttons(ui, 48.0);
                    }
                    if !compact {
                        self.navigation(ui);
                    }
                });
            });
        });
        egui::Panel::bottom("jirka-footer").frame(egui::Frame::new().fill(t.bg).inner_margin(egui::Margin::symmetric(20, 9))).show(ui, |ui| {
            widgets::hud_elided(ui, &self.message, t.faint, ui.available_width());
        });
        egui::CentralPanel::default().frame(egui::Frame::new().fill(t.bg).inner_margin(egui::Margin::symmetric(20, 18))).show(ui, |ui| {
            // The toolbar shares the vertical scroller at small heights and large UI scales.
            egui::ScrollArea::vertical().auto_shrink(false).show(ui, |ui| {
                if compact {
                    ui.horizontal_wrapped(|ui| self.navigation(ui));
                    ui.add_space(10.0);
                }
                self.toolbar(ui);
                ui.add_space(22.0);
                let entries = self.visible_entries();
                self.card_bounds.clear();
                self.action_bounds.clear();
                if entries.is_empty() {
                    ui.add_space(30.0);
                    ui.label("V tomto pohledu nejsou žádné aplikace.");
                } else if self.preferences.table {
                    self.table(ui, &entries);
                } else {
                    self.grid(ui, &entries);
                }
            });
        });
        self.dialogs(&ctx);
        if custom_title {
            widgets::window_edges(&ctx);
        }
    }
    fn navigation(&mut self, ui: &mut Ui) {
        let order = if ui.layout().prefer_right_to_left() { [4, 3, 2, 1, 0] } else { [0, 1, 2, 3, 4] };
        for item in order {
            match item {
                0 | 1 => {
                    let table = item == 1;
                    let label = if table { "Tabulka" } else { "Mřížka" };
                    if widgets::nav_link(ui, label, label, 40.0, self.preferences.table == table).clicked() {
                        self.preferences.table = table;
                    }
                }
                2 => {
                    if widgets::nav_icon(ui, "⟳", "Obnovit katalog", 40.0, self.busy).clicked() {
                        self.send("refresh", None);
                    }
                }
                3 => {
                    if widgets::nav_link(ui, "Vzhled", "Světlý / tmavý vzhled", 40.0, false).clicked() {
                        self.preferences.dark = !self.preferences.dark;
                    }
                }
                _ => {
                    if widgets::nav_icon(ui, "⚙", "Nastavení", 40.0, false).clicked() {
                        self.settings_open = true;
                        self.source_edit = self.source.clone();
                    }
                }
            }
        }
    }
    fn toolbar(&mut self, ui: &mut Ui) {
        let t = Tokens::get(ui.ctx());
        let options: Vec<_> = ["Vše", "Nainstalované", "Aktualizace", "Nenainstalované"]
            .iter()
            .enumerate()
            .map(|(f, label)| {
                let count = self
                    .entries
                    .iter()
                    .filter(|e| self.matches_filter(e, f) && (self.category.is_empty() || e.app.category == self.category))
                    .count();
                (f, Cell { label, tip: label, count: Some((count, (f == 2).then_some(t.warn))) })
            })
            .collect();
        // cells() draws an indivisible group; split it into rows before laying it out.
        let available = ui.available_width();
        let mut start = 0;
        let mut row_width = 0.0;
        for (index, (_, cell)) in options.iter().enumerate() {
            let count = cell.count.map_or(0, |(count, _)| count);
            let width = widgets::hud_galley(ui, cell.label, t.ink).size().x + widgets::hud_galley(ui, &count.to_string(), t.ink).size().x + 32.0;
            if index > start && row_width + width > available {
                ui.horizontal(|ui| widgets::cells(ui, &mut self.filter, &options[start..index]));
                start = index;
                row_width = 0.0;
            }
            row_width += width;
        }
        ui.horizontal(|ui| widgets::cells(ui, &mut self.filter, &options[start..]));
        ui.add_space(12.0);
        ui.horizontal_wrapped(|ui| {
            if widgets::nav_link(ui, "Všechny platformy", "Všechny platformy", 34.0, self.category.is_empty()).clicked() {
                self.category.clear();
            }
            for (id, name) in CATEGORIES {
                if widgets::nav_link(ui, name, name, 34.0, self.category == id).clicked() {
                    self.category = id.into();
                }
            }
        });
        ui.add_space(8.0);
        let narrow_search = ui.available_width() < 460.0;
        let search_controls = |ui: &mut Ui| {
            let width = ui.available_width().min(240.0);
            ui.add(
                egui::TextEdit::singleline(&mut self.search)
                    .hint_text("Hledat aplikace…")
                    .desired_width(width)
                    .min_size(vec2(0.0, 34.0))
                    .margin(vec2(10.0, 7.0)),
            );
            egui::ComboBox::from_id_salt("jirka-sort")
                .selected_text(if self.alphabetical { "Podle názvu" } else { "Pořadí katalogu" })
                .width(150.0)
                .show_ui(ui, |ui| {
                    ui.selectable_value(&mut self.alphabetical, false, "Pořadí katalogu");
                    ui.selectable_value(&mut self.alphabetical, true, "Podle názvu");
                });
            let updates: Vec<_> = self.entries.iter().filter(|e| e.state == "update").map(|e| e.app.id.clone()).collect();
            if !updates.is_empty() && ui.add_enabled_ui(!self.busy, |ui| widgets::button(ui, "Aktualizovat vše", Kind::Primary, 0.0)).inner.clicked()
            {
                self.confirm = Some(updates);
            }
            if self.busy && widgets::button(ui, "Zrušit", Kind::Secondary, 0.0).clicked() {
                self.send("cancel", None);
            }
        };
        if narrow_search {
            ui.vertical(search_controls);
        } else {
            ui.horizontal_wrapped(search_controls);
        }
    }
    fn grid(&mut self, ui: &mut Ui, entries: &[Entry]) {
        let width = ui.available_width();
        let cols = ((width / 320.0).floor() as usize).clamp(1, 6);
        let card_w = (width + (cols as f32 - 1.0)) / cols as f32;
        for (index, (category, label)) in CATEGORIES.iter().enumerate() {
            let list: Vec<_> = entries.iter().filter(|e| e.app.category == *category).collect();
            if list.is_empty() {
                continue;
            }
            widgets::eyebrow(ui, &format!("{:02}", index + 1), label);
            ui.add_space(10.0);
            for row in list.chunks(cols) {
                let height = row.iter().map(|e| card_height(ui, e, card_w)).fold(280.0f32, f32::max);
                let (area, _) = ui.allocate_exact_size(vec2(width, height), Sense::hover());
                for (col, entry) in row.iter().enumerate() {
                    let rect = Rect::from_min_size(area.min + vec2(col as f32 * (card_w - 1.0), 0.0), vec2(card_w, height));
                    self.card(ui, entry, rect);
                }
                ui.add_space(-ui.spacing().item_spacing.y - 1.0);
            }
            ui.add_space(28.0);
        }
    }
    fn card(&mut self, ui: &mut Ui, entry: &Entry, rect: Rect) {
        let t = Tokens::get(ui.ctx());
        let color = category_color(&entry.app.category);
        let painter = ui.painter_at(rect);
        painter.rect(rect, egui::CornerRadius::ZERO, t.bg, t.hairline(), egui::StrokeKind::Inside);
        let hovered = ui.rect_contains_pointer(rect);
        let lift = ui.ctx().animate_bool_with_time(ui.id().with(("card-hover", &entry.app.id)), hovered, 0.3);
        if lift > 0.0 {
            painter.rect_filled(Rect::from_min_size(rect.min, vec2(rect.width() * lift, 2.0)), egui::CornerRadius::ZERO, color);
        }
        let strip = Rect::from_min_size(rect.min, vec2(rect.width(), 40.0));
        painter.hline(strip.x_range(), strip.bottom() - 0.5, t.hairline());
        let number = self.entries.iter().position(|e| e.app.id == entry.app.id).map_or_else(String::new, |i| format!("{:02}", i + 1));
        let mut strip_ui = ui.new_child(
            egui::UiBuilder::new()
                .id_salt((&entry.app.id, "strip"))
                .max_rect(strip.shrink2(vec2(20.0, 0.0)))
                .layout(egui::Layout::left_to_right(egui::Align::Center)),
        );
        widgets::tag(&mut strip_ui, &number, color, Color32::BLACK);
        strip_ui.with_layout(egui::Layout::right_to_left(egui::Align::Center), |ui| {
            if !entry.app.repository.is_empty() {
                widgets::hud_link(ui, "GitHub", &entry.app.repository);
            }
        });
        let inner = Rect::from_min_max(rect.min + vec2(20.0, 58.0), rect.max - vec2(20.0, 20.0));
        let mut child = ui.new_child(egui::UiBuilder::new().id_salt(&entry.app.id).max_rect(inner).layout(egui::Layout::top_down(egui::Align::Min)));
        child.set_clip_rect(rect.intersect(ui.clip_rect()));
        child.horizontal_top(|ui| {
            app_icon(ui, entry, 56.0);
            ui.add_space(6.0);
            ui.vertical(|ui| {
                ui.set_width((inner.width() - 70.0).max(40.0));
                ui.add(egui::Label::new(RichText::new(&entry.app.name).font(FontId::new(22.0, theme::display())).color(t.ink_strong)).wrap());
                ui.add(egui::Label::new(RichText::new(category_label(&entry.app.category)).size(12.0).color(t.faint)).wrap());
            });
        });
        child.add_space(10.0);
        child.add(egui::Label::new(RichText::new(&entry.app.description).size(13.5).color(t.muted)).wrap());
        child.add_space(12.0);
        widgets::badge(&mut child, status_label(entry), status_color(entry, &t));
        child.add_space(3.0);
        child.add(egui::Label::new(RichText::new(version_label(entry)).size(12.5).color(t.muted)).wrap());
        let text_bounds = child.min_rect();
        self.card_bounds.push((entry.app.id.clone(), inner, text_bounds));
        let action = child.with_layout(egui::Layout::bottom_up(egui::Align::Min), |ui| {
            ui.horizontal(|ui| self.actions(ui, entry, inner.width()));
        });
        self.action_bounds.push((entry.app.id.clone(), inner, action.response.rect));
    }
    fn table(&mut self, ui: &mut Ui, entries: &[Entry]) {
        use egui_extras::{Column, TableBuilder};
        let t = Tokens::get(ui.ctx());
        let row_heights: Vec<_> =
            entries.iter().map(|e| (wrapped_height(ui, &e.app.name, FontId::new(16.0, theme::display()), 220.0) + 36.0).max(74.0)).collect();
        egui::ScrollArea::horizontal().auto_shrink([false, true]).show(ui, |ui| {
            ui.set_min_width(1000.0);
            egui::Frame::new().fill(t.bg).stroke(t.hairline()).inner_margin(egui::Margin::symmetric(16, 0)).show(ui, |ui| {
                let clip = ui.clip_rect();
                let span = ui.available_rect_before_wrap().x_range();
                TableBuilder::new(ui)
                    .striped(false)
                    .vscroll(false)
                    .cell_layout(egui::Layout::left_to_right(egui::Align::Center))
                    .column(Column::remainder().at_least(280.0))
                    .column(Column::exact(150.0))
                    .column(Column::exact(170.0))
                    .column(Column::exact(100.0))
                    .column(Column::exact(220.0))
                    .header(42.0, |mut header| {
                        for title in ["Aplikace", "Platforma", "Stav", "Verze", "Akce"] {
                            header.col(|ui| {
                                widgets::hud(ui, title, t.faint);
                            });
                        }
                    })
                    .body(|mut body| {
                        for (entry, height) in entries.iter().zip(&row_heights) {
                            body.row(*height, |mut row| {
                                row.col(|ui| {
                                    app_icon(ui, entry, 38.0);
                                    ui.vertical(|ui| {
                                        ui.add(egui::Label::new(RichText::new(&entry.app.name).font(FontId::new(16.0, theme::display()))).wrap());
                                        ui.add(egui::Label::new(RichText::new(&entry.app.description).size(12.0).color(t.muted)).truncate())
                                            .on_hover_text(&entry.app.description);
                                    });
                                    let y = ui.max_rect().bottom() + ui.spacing().item_spacing.y / 2.0;
                                    ui.ctx().layer_painter(ui.layer_id()).with_clip_rect(clip).hline(span, y, t.hairline());
                                });
                                row.col(|ui| {
                                    ui.add(egui::Label::new(category_label(&entry.app.category)).wrap());
                                });
                                row.col(|ui| {
                                    ui.add(egui::Label::new(RichText::new(status_label(entry)).color(status_color(entry, &t))).wrap());
                                });
                                row.col(|ui| {
                                    ui.add(egui::Label::new(&entry.app.version).wrap());
                                });
                                row.col(|ui| self.actions(ui, entry, 218.0));
                            });
                        }
                    });
            });
        });
    }
    fn actions(&mut self, ui: &mut Ui, entry: &Entry, width: f32) {
        if self.busy && self.active_id == entry.app.id {
            widgets::progress(ui, None, "Probíhá instalace…", category_color(&entry.app.category), (width - 42.0).max(100.0));
            if widgets::glyph_button(ui, widgets::Glyph::Close, "Zrušit instalaci").clicked() {
                self.send("cancel", None);
            }
            return;
        }
        let (label, command) = match (entry.app.kind.as_str(), entry.state.as_str()) {
            ("portable-zip", "installed") => ("Spustit", "launch"),
            ("portable-zip", "update") => ("Aktualizovat", "install"),
            ("portable-zip", "available") => ("Instalovat", "install"),
            ("ha-addon", _) => ("Otevřít v HA", "open"),
            ("esp-web", _) => ("Nahrát firmware", "open"),
            ("link", _) => ("Otevřít", "open"),
            ("apk", _) => ("Pro Android", ""),
            (_, "broken") => ("Chyba instalace", ""),
            _ => ("Připravuje se", ""),
        };
        let primary = ui.add_enabled_ui(!self.busy && !command.is_empty(), |ui| {
            widgets::button(ui, label, if command == "launch" { Kind::Secondary } else { Kind::Primary }, (width - 50.0).max(100.0))
        });
        if primary.inner.clicked() {
            if command == "install" {
                self.confirm = Some(vec![entry.app.id.clone()]);
            } else {
                self.send(command, Some(&entry.app.id));
            }
        }
        widgets::more_menu(ui, |ui| {
            if ui.button("Podrobnosti").clicked() {
                self.detail = Some(entry.clone());
                ui.close();
            }
            if !entry.app.repository.is_empty() && ui.button("Projekt na GitHubu ↗").clicked() {
                self.send("repo", Some(&entry.app.id));
                ui.close();
            }
            if Self::installed(entry) && ui.button("Spustit nainstalovanou verzi").clicked() {
                self.send("launch", Some(&entry.app.id));
                ui.close();
            }
        });
    }
    fn dialogs(&mut self, ctx: &egui::Context) {
        let width = (ctx.content_rect().width() - 48.0).clamp(240.0, 620.0);
        if self.settings_open {
            let mut open = true;
            egui::Window::new("Nastavení").open(&mut open).default_width(width).max_width(width).resizable(false).vscroll(true).show(ctx, |ui| {
                ui.label("Zdroj katalogu · HTTPS adresa nebo místní JSON");
                ui.add(egui::TextEdit::singleline(&mut self.source_edit).desired_width(f32::INFINITY));
                ui.horizontal_wrapped(|ui| {
                    if ui.add_enabled(!self.busy, egui::Button::new("Načíst katalog")).clicked() {
                        self.commands.push(Command { command: "source".into(), id: None, source: Some(self.source_edit.clone()) });
                        self.settings_open = false;
                    }
                    if ui.button("Výchozí online katalog").clicked() {
                        self.source_edit = DEFAULT_SOURCE.into();
                    }
                    if ui.button("Složka aplikací").clicked() {
                        self.send("folder", None);
                    }
                });
                ui.separator();
                ui.checkbox(&mut self.preferences.dark, "Tmavý vzhled");
                ui.add(egui::Slider::new(&mut self.preferences.scale, 0.8..=1.5).text("Měřítko rozhraní"));
                ui.separator();
                ui.label("Jirka Store 1.1.0 · odvozeno ze zdrojového kódu ArtCraft Launcheru");
                ui.hyperlink_to("Původní projekt a licence", "https://github.com/storytold/craft-launcher");
            });
            if !open {
                self.settings_open = false;
            }
        }
        if let Some(ids) = self.confirm.clone() {
            egui::Window::new("Potvrdit instalaci").default_width(width).max_width(width).collapsible(false).resizable(false).vscroll(true).show(
                ctx,
                |ui| {
                    for id in &ids {
                        if let Some(e) = self.entries.iter().find(|e| &e.app.id == id) {
                            ui.label(format!("{} · {}", e.app.name, e.app.version));
                        }
                    }
                    ui.add(egui::Label::new(format!("Zdroj: {}", self.loaded_source)).wrap());
                    ui.label("Balíček se ověří pomocí SHA-256. Předchozí verze a její data zůstanou zachované.");
                    ui.horizontal_wrapped(|ui| {
                        if widgets::button(ui, "Instalovat", Kind::Primary, 0.0).clicked() {
                            for id in &ids {
                                self.send("install", Some(id));
                            }
                            self.confirm = None;
                        }
                        if widgets::button(ui, "Zpět", Kind::Secondary, 0.0).clicked() {
                            self.confirm = None;
                        }
                    });
                },
            );
        }
        if let Some(entry) = self.detail.clone() {
            let mut open = true;
            egui::Window::new("Podrobnosti aplikace").open(&mut open).default_width(width).max_width(width).resizable(false).vscroll(true).show(
                ctx,
                |ui| {
                    ui.heading(&entry.app.name);
                    ui.label(category_label(&entry.app.category));
                    ui.add(egui::Label::new(&entry.app.description).wrap());
                    ui.label(version_label(&entry));
                    ui.label(match entry.app.kind.as_str() {
                        "apk" => "Instalujte pomocí Jirka Store na Androidu.",
                        "ha-addon" => "Instalaci a aktualizace spravuje Home Assistant.",
                        "esp-web" => "Připojte desku USB a potvrďte firmware v prohlížeči.",
                        "portable-zip" => "Uživatelská data patří do AppData. Data vedle EXE se mezi verzemi automaticky nekopírují.",
                        _ => "",
                    });
                    if !entry.app.repository.is_empty() {
                        ui.hyperlink_to("Projekt na GitHubu", &entry.app.repository);
                    }
                },
            );
            if !open {
                self.detail = None;
            }
        }
        if let Some(error) = self.error.clone() {
            let mut open = true;
            egui::Window::new("Operaci se nepodařilo dokončit").open(&mut open).default_width(width).max_width(width).vscroll(true).show(ctx, |ui| {
                ui.add(egui::Label::new(error).wrap());
            });
            if !open {
                self.error = None;
            }
        }
    }
}

fn category_label(id: &str) -> &str {
    CATEGORIES.iter().find(|(key, _)| *key == id).map_or("Ostatní", |(_, title)| *title)
}
fn category_color(id: &str) -> Color32 {
    let rgb = match id {
        "pc" => [47, 123, 245],
        "esp32" => [242, 165, 22],
        "homeassistant" => [18, 165, 138],
        "android" => [139, 92, 246],
        "web" => [224, 54, 143],
        _ => [123, 181, 28],
    };
    Color32::from_rgb(rgb[0], rgb[1], rgb[2])
}
fn status_label(e: &Entry) -> &'static str {
    match e.state.as_str() {
        "installed" => "Nainstalováno · aktuální",
        "update" => "Dostupná aktualizace",
        "available" => "Připraveno k instalaci",
        "external" => "Externí instalátor",
        "android" => "Instalace v Androidu",
        "broken" => "Poškozená instalace",
        _ => "Připravuje se",
    }
}
fn status_color(e: &Entry, t: &Tokens) -> Color32 {
    match e.state.as_str() {
        "installed" => t.good,
        "update" => t.warn,
        "broken" => t.bad,
        "available" | "external" => t.accent_ink,
        _ => t.muted,
    }
}
fn version_label(e: &Entry) -> String {
    if e.state == "update" { format!("{} nainstalováno → {}", e.installed_version, e.app.version) } else { format!("Verze {}", e.app.version) }
}
fn wrapped_height(ui: &Ui, text: &str, font: FontId, width: f32) -> f32 {
    ui.painter().layout(text.to_string(), font, Color32::WHITE, width.max(40.0)).size().y
}
fn card_height(ui: &Ui, e: &Entry, width: f32) -> f32 {
    let heading = wrapped_height(ui, &e.app.name, FontId::new(22.0, theme::display()), width - 110.0) + 28.0;
    let description = wrapped_height(ui, &e.app.description, FontId::proportional(13.5), width - 40.0);
    let version = wrapped_height(ui, &version_label(e), FontId::proportional(12.5), width - 40.0);
    58.0 + heading.max(56.0) + description + version + 124.0
}
fn app_icon(ui: &mut Ui, e: &Entry, size: f32) {
    let color = category_color(&e.app.category);
    let (rect, r) = ui.allocate_exact_size(vec2(size, size), Sense::hover());
    ui.painter().rect_filled(rect, egui::CornerRadius::same(10), color);
    let glyph = match e.app.category.as_str() {
        "pc" => "PC",
        "esp32" => "E",
        "homeassistant" => "H",
        "android" => "A",
        _ => "J",
    };
    ui.painter().text(rect.center(), egui::Align2::CENTER_CENTER, glyph, FontId::new(size * 0.45, theme::display()), Color32::BLACK);
    r.widget_info(|| egui::WidgetInfo::labeled(egui::WidgetType::Image, true, &e.app.name));
}
