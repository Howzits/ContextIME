"""Build a dependency-free VSIX (OPC ZIP) from the explicit runtime file list."""
import json
from pathlib import Path
from zipfile import ZipFile, ZIP_DEFLATED
from xml.sax.saxutils import escape

root = Path(__file__).resolve().parent
package = json.loads((root / 'package.json').read_text(encoding='utf-8'))
version = escape(package['version'])
manifest = f'''<?xml version="1.0" encoding="utf-8"?>
<PackageManifest Version="2.0.0" xmlns="http://schemas.microsoft.com/developer/vsx-schema/2011">
  <Metadata>
    <Identity Language="en-US" Id="{package['name']}" Version="{version}" Publisher="{package['publisher']}" />
    <DisplayName>{escape(package['displayName'])}</DisplayName>
    <Description xml:space="preserve">{escape(package['description'])}</Description>
    <Tags>{escape(','.join(package.get('keywords', [])))}</Tags><Categories>Other</Categories>
    <Properties>
      <Property Id="Microsoft.VisualStudio.Code.Engine" Value="{escape(package['engines']['vscode'])}" />
      <Property Id="Microsoft.VisualStudio.Code.ExtensionKind" Value="ui" />
    </Properties>
  </Metadata>
  <Installation><InstallationTarget Id="Microsoft.VisualStudio.Code" /></Installation>
  <Dependencies />
  <Assets>
    <Asset Type="Microsoft.VisualStudio.Code.Manifest" Path="extension/package.json" Addressable="true" />
    <Asset Type="Microsoft.VisualStudio.Services.Content.Details" Path="extension/README.md" Addressable="true" />
    <Asset Type="Microsoft.VisualStudio.Services.Content.License" Path="extension/LICENSE" Addressable="true" />
    <Asset Type="Microsoft.VisualStudio.Services.Content.Changelog" Path="extension/CHANGELOG.md" Addressable="true" />
  </Assets>
</PackageManifest>'''
content_types = '''<?xml version="1.0" encoding="utf-8"?>
<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
<Default Extension="json" ContentType="application/json" />
<Default Extension="js" ContentType="application/javascript" />
<Default Extension="md" ContentType="text/markdown" />
<Default Extension="exe" ContentType="application/octet-stream" />
<Default Extension="vsixmanifest" ContentType="text/xml" />
<Default Extension="txt" ContentType="text/plain" />
<Override PartName="/extension/LICENSE" ContentType="text/plain" />
</Types>'''
destination = root / 'dist' / f"ContextIME-{package['version']}.vsix"
destination.parent.mkdir(exist_ok=True)
with ZipFile(destination, 'w', ZIP_DEFLATED) as archive:
    archive.writestr('extension.vsixmanifest', manifest)
    archive.writestr('[Content_Types].xml', content_types)
    for name in ['package.json', 'extension.js', 'README.md', 'CHANGELOG.md', 'LICENSE', 'bin/InputHelper.exe', 'docs/COMPATIBILITY.md', 'docs/USAGE.md']:
        archive.write(root / name, 'extension/' + name)
with ZipFile(destination) as archive:
    assert archive.testzip() is None
    assert json.loads(archive.read('extension/package.json'))['displayName'] == 'ContextIME'
print(destination)
