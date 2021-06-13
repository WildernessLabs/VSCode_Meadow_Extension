
$cmd = $args[0];

if (-Not $cmd)
{
	$cmd = "debug"
}

function Vsix
{
	Write-Host "Creating VSIX..."
	& ./node_modules/.bin/vsce package
	Write-Host "Done."
}
function BuildNet
{
	Write-Host "Building .NET Project..."

	& dotnet build -p:Configuration=Debug ./src/csharp/VSCodeMeadow.csproj

	Write-Host "Done .NET Project "
}

function BuildTypeScript
{
	Write-Host "Building WebPack..."

	& npm run webpack

	Write-Host "Done WebPack."
}


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