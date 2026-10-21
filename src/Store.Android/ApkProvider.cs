using Android.Content;
using Android.Database;
using Android.OS;
using Uri = Android.Net.Uri;

namespace Jirka.Store.AndroidApp;

[ContentProvider(new[] { "cz.jirkas.store.files" }, Exported = false, GrantUriPermissions = true)]
public sealed class ApkProvider : ContentProvider
{
    public override bool OnCreate() => true;
    string Resolve(Uri uri)
    {
        var name = uri.LastPathSegment ?? "";
        if (uri.Authority != "cz.jirkas.store.files" || uri.Path != "/" + name || !System.Text.RegularExpressions.Regex.IsMatch(name, "^[a-f0-9]{32}\\.apk$"))
            throw new Java.IO.FileNotFoundException("Neplatný soubor APK.");
        return System.IO.Path.Combine(Context!.CacheDir!.AbsolutePath, name);
    }
    public override ParcelFileDescriptor? OpenFile(Uri uri, string mode)
    {
        if (mode != "r") throw new Java.Lang.SecurityException("Pouze čtení.");
        return ParcelFileDescriptor.Open(new Java.IO.File(Resolve(uri)), ParcelFileMode.ReadOnly);
    }
    public override string GetType(Uri uri) => "application/vnd.android.package-archive";
    public override ICursor Query(Uri uri, string[]? projection, string? selection, string[]? selectionArgs, string? sortOrder)
    {
        var file = new FileInfo(Resolve(uri)); var columns = projection ?? ["_display_name", "_size"];
        var cursor = new MatrixCursor(columns);
        cursor.AddRow(columns.Select(c => c == "_display_name" ? (Java.Lang.Object)new Java.Lang.String(file.Name) : c == "_size" ? Java.Lang.Long.ValueOf(file.Length) : new Java.Lang.String("")).ToArray());
        return cursor;
    }
    public override Uri? Insert(Uri uri, ContentValues? values) => throw new Java.Lang.SecurityException("Pouze čtení.");
    public override int Delete(Uri uri, string? selection, string[]? selectionArgs) => throw new Java.Lang.SecurityException("Pouze čtení.");
    public override int Update(Uri uri, ContentValues? values, string? selection, string[]? selectionArgs) => throw new Java.Lang.SecurityException("Pouze čtení.");
}
