fn main() {
    if std::env::var("CARGO_CFG_TARGET_OS").as_deref() == Ok("windows") {
        let mut resource = winresource::WindowsResource::new();
        resource.set_icon("../../../assets/store.ico");
        resource.set("ProductName", "Jirka Store");
        resource.set("FileDescription", "Jirka Store — ArtCraft source fork");
        if let Err(error) = resource.compile() {
            eprintln!("Resource compilation: {error}");
            std::process::exit(1);
        }
    }
}
