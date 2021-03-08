
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

	& msbuild /r /p:Configuration=Debug /p:nugetInteractive=true ./src/csharp/VSCodeMeadow.csproj

	Write-Host "Done .NET Project "
}

function BuildTypeScript
{
	Write-Host "Building TypeScript..."

	& tsc -p ./src/typescript

	Write-Host "Done TypeScript."
}


switch ($cmd) {
	"all" {
		Debug
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