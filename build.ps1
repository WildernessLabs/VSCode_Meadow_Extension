
$cmd = $args[0];

if (-Not $cmd)
{
	$cmd = "debug"
}

function Vsix
{
	Write-Host "Creating VSIX..."
	& npm install -g vsce
	& vsce package
	Write-Host "Done VSIX"
}
function BuildNet
{
	Write-Host "Building .NET Project..."

	& dotnet build -p:Configuration=Release ./src/csharp/VSCodeMeadow.sln

	Write-Host "Done .NET Project "
}

function BuildTypeScript
{
	Write-Host "Building WebPack..."

	& npm install -g webpack
	& npm run webpack

	Write-Host "Done WebPack."
}

npm install

switch ($cmd) {
	"all" {
		BuildNet
		BuildTypeScript
		Vsix
	}
	"vsix" {
		BuildNet
		BuildTypeScript
		Vsix
	}
	"build" {
		BuildNet
		BuildTypeScript
	}
	"debug" {
		BuildNet
		BuildTypeScript
	}
	"ts" {
		BuildTypeScript
	}
	"net" {
		BuildNet
	}
}