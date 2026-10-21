//! Desktop shell adapted from apps/artcraft-launcher/src/main.rs.
#![cfg_attr(windows, windows_subsystem = "windows")]
#![deny(clippy::unwrap_used, clippy::expect_used, clippy::panic)]

use artcraft_launcher_ui_egui::jirka::{Command, Event, JirkaUi, Preferences};
use std::{
    collections::VecDeque,
    io::{BufRead, BufReader, Write},
    process::{Child, ChildStdin, Command as Process, Stdio},
    sync::mpsc::{self, Receiver},
    time::{Duration, Instant},
};

struct Shell {
    ui: JirkaUi,
    child: Option<Child>,
    input: Option<ChildStdin>,
    events: Receiver<Event>,
    installs: VecDeque<Command>,
    installing: bool,
    refresh_at: Instant,
}
impl Shell {
    fn new(cc: &eframe::CreationContext<'_>) -> Self {
        let preferences = cc.storage.and_then(|s| eframe::get_value::<Preferences>(s, "jirka-preferences")).unwrap_or_default();
        let mut ui = JirkaUi::new(&cc.egui_ctx, preferences);
        let (tx, events) = mpsc::channel();
        let engine = std::env::current_exe().ok().and_then(|p| p.parent().map(|p| p.join("engine/JirkaStore.Host.exe")));
        let mut child = None;
        let mut input = None;
        if let Some(engine) = engine {
            let mut command = Process::new(engine);
            command.stdin(Stdio::piped()).stdout(Stdio::piped()).stderr(Stdio::null());
            #[cfg(windows)]
            {
                use std::os::windows::process::CommandExt;
                command.creation_flags(0x08000000);
            }
            match command.spawn() {
                Ok(mut process) => {
                    input = process.stdin.take();
                    if let Some(stdout) = process.stdout.take() {
                        let ctx = cc.egui_ctx.clone();
                        std::thread::spawn(move || {
                            for line in BufReader::new(stdout).lines() {
                                match line.and_then(|line| serde_json::from_str::<Event>(&line).map_err(std::io::Error::other)) {
                                    Ok(event) => {
                                        if tx.send(event).is_err() {
                                            return;
                                        }
                                        ctx.request_repaint();
                                    }
                                    Err(error) => {
                                        let _ = tx.send(Event { r#type: "error".into(), message: error.to_string(), ..Event::default() });
                                        ctx.request_repaint();
                                    }
                                }
                            }
                            let _ = tx.send(Event {
                                r#type: "error".into(),
                                message: "Instalační služba byla ukončena. Spusťte launcher znovu.".into(),
                                ..Event::default()
                            });
                            ctx.request_repaint();
                        });
                    }
                    child = Some(process);
                }
                Err(error) => {
                    ui.error = Some(format!("Nelze spustit instalační službu: {error}. Rozbalte celý balíček včetně složky engine."));
                }
            }
        }
        Self { ui, child, input, events, installs: VecDeque::new(), installing: false, refresh_at: Instant::now() }
    }
    fn send(&mut self, command: Command) {
        let result = (|| -> Result<(), Box<dyn std::error::Error>> {
            let input = self.input.as_mut().ok_or("Instalační služba není dostupná")?;
            serde_json::to_writer(&mut *input, &command)?;
            input.write_all(b"\n")?;
            input.flush()?;
            Ok(())
        })();
        if let Err(error) = result {
            self.installing = false;
            self.installs.clear();
            self.ui.error = Some(error.to_string());
        }
    }
}
impl eframe::App for Shell {
    fn logic(&mut self, ctx: &egui::Context, _frame: &mut eframe::Frame) {
        while let Ok(event) = self.events.try_recv() {
            if event.r#type == "snapshot" && !event.busy {
                self.installing = false;
            }
            if event.r#type == "error" {
                self.installs.clear();
            }
            self.ui.accept(event);
        }
        if self.installing && self.ui.message.starts_with("Operace zrušena") {
            self.installs.clear();
        }
        if !self.installing
            && !self.ui.busy
            && let Some(command) = self.installs.pop_front()
        {
            self.installing = true;
            self.send(command);
        }
        if !self.ui.busy && self.refresh_at.elapsed() > Duration::from_secs(1800) {
            self.refresh_at = Instant::now();
            self.send(Command { command: "refresh".into(), id: None, source: None });
        }
        ctx.set_zoom_factor(self.ui.preferences.scale.clamp(0.8, 1.5));
        ctx.request_repaint_after(Duration::from_secs(30));
    }
    fn ui(&mut self, ui: &mut egui::Ui, _frame: &mut eframe::Frame) {
        self.ui.ui(ui, true);
        for command in self.ui.drain_commands() {
            if command.command == "install" {
                self.installs.push_back(command);
            } else {
                if command.command == "cancel" {
                    self.installs.clear();
                }
                self.send(command);
            }
        }
        if !self.installs.is_empty() {
            ui.ctx().request_repaint_after(Duration::from_millis(50));
        }
    }
    fn save(&mut self, storage: &mut dyn eframe::Storage) {
        eframe::set_value(storage, "jirka-preferences", &self.ui.preferences);
    }
}
impl Drop for Shell {
    fn drop(&mut self) {
        // EOF asks the child to cancel safely and finish extraction cleanup before it exits.
        self.input.take();
        if let Some(mut child) = self.child.take() {
            std::thread::spawn(move || {
                let _ = child.wait();
            });
        }
    }
}
fn main() -> eframe::Result {
    env_logger::init();
    let mut viewport = egui::ViewportBuilder::default()
        .with_title("Jirka Store")
        .with_app_id("cz.jirkas.store")
        .with_inner_size([1180.0, 820.0])
        .with_min_inner_size([520.0, 360.0])
        .with_decorations(false);
    if let Ok(icon) = eframe::icon_data::from_png_bytes(include_bytes!("../../../../assets/store.png")) {
        viewport = viewport.with_icon(icon);
    }
    let mut wgpu_options = eframe::WgpuConfiguration::default();
    if let eframe::egui_wgpu::WgpuSetup::CreateNew(setup) = &mut wgpu_options.wgpu_setup {
        setup.instance_descriptor.backends = eframe::wgpu::Backends::DX12 | eframe::wgpu::Backends::GL;
    }
    eframe::run_native(
        "Jirka Store",
        eframe::NativeOptions { viewport, wgpu_options, ..Default::default() },
        Box::new(|cc| Ok(Box::new(Shell::new(cc)))),
    )
}
