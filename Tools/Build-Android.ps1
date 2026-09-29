param(
    [string]$UnityPath = 'C:\Program Files\Unity\Hub\Editor\6000.3.25f1\Editor\Unity.exe',
    [switch]$PackageOnly
)
$ErrorActionPreference = 'Stop'
$projectRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if (-not (Test-Path -LiteralPath $UnityPath)) { throw 'Nie znaleziono Unity. Podaj -UnityPath.' }
if (Get-Process Unity -ErrorAction SilentlyContinue) { throw 'Zamknij edytor Unity przed budowaniem z tego skryptu.' }
Add-Type -TypeDefinition @'
using System.Runtime.InteropServices;
using System.Text;
public static class PasjansBuildPaths {
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    public static extern uint GetShortPathName(string path, StringBuilder result, int length);
}
'@
$shortBuffer = [System.Text.StringBuilder]::new(32768)
if ([PasjansBuildPaths]::GetShortPathName($projectRoot, $shortBuffer, $shortBuffer.Capacity) -eq 0) {
    throw 'Nie można uzyskać krótkiej ścieżki projektu.'
}
$shortProject = $shortBuffer.ToString()
if ($shortProject -match '[^\x00-\x7F]') { throw 'Android wymaga ścieżki projektu bez polskich znaków.' }
$logDirectory = Join-Path $projectRoot 'TestResults'
[System.IO.Directory]::CreateDirectory($logDirectory) | Out-Null
$previousOptions = $env:JAVA_TOOL_OPTIONS
$previousGradleHome = $env:GRADLE_USER_HOME
$previousCodePage = [regex]::Match((& $env:ComSpec /d /c chcp), '\d+').Value
$buildDrive = $null
try {
    # Na tym komputerze połączenia AF_UNIX zawodzą w PipeImpl NIO z JDK 17.
    # NUL nie może przechowywać gniazd, więc JDK używa wbudowanego połączenia TCP.
    # Ustaw przed startem Unity, bo Unity przekazuje to środowisko narzędziom potomnym.
    # Zmiana dotyczy tylko tego procesu i nie modyfikuje zmiennych systemowych.
    $env:JAVA_TOOL_OPTIONS = (($previousOptions + ' -Djdk.net.unixdomain.tmpdir=NUL -Dfile.encoding=UTF-8').Trim())
    # Android tworzy pliki .bat w UTF-8; strona kodowa 852 zniekształca polskie znaki
    # w ścieżkach cache Gradle, np. w nazwie użytkownika Paweł.
    & $env:ComSpec /d /c chcp 65001 | Out-Null
    $usedDrives = [System.IO.Directory]::GetLogicalDrives()
    foreach ($letter in @('P','Q','R','S','T','U','V','W','X','Y','Z')) {
        if ($usedDrives -notcontains ($letter + ':\')) { $buildDrive = $letter + ':'; break }
    }
    if (-not $buildDrive) { throw 'Brak wolnej litery dysku dla tymczasowej ścieżki kompilacji.' }
    & subst $buildDrive $projectRoot
    if ($LASTEXITCODE -ne 0) { throw 'Nie można utworzyć tymczasowej ścieżki kompilacji.' }
    $asciiProject = $buildDrive + '\'
    $asciiGradleHome = Join-Path $asciiProject 'Library\GradleHome'
    [System.IO.Directory]::CreateDirectory($asciiGradleHome) | Out-Null
    $sourceModules = Join-Path $env:USERPROFILE '.gradle\caches\modules-2'
    $targetModules = Join-Path $asciiGradleHome 'caches\modules-2'
    if (Test-Path -LiteralPath $sourceModules) {
        & robocopy $sourceModules $targetModules /E /XF '*.lock' '*.lck' /NFL /NDL /NJH /NJS /NP | Out-Null
        if ($LASTEXITCODE -gt 7) { throw 'Nie można przygotować bezpiecznego cache Gradle.' }
    }
    $env:GRADLE_USER_HOME = $asciiGradleHome
    $buildArguments = @('-batchmode', '-nographics', '-quit', '-projectPath', ('"' + $shortProject + '"'),
        '-buildTarget', 'Android', '-executeMethod', 'Pasjans.Editor.BuildGame.Android',
        '-logFile', ('"' + (Join-Path $shortProject 'TestResults\build-android.log') + '"'))
    if (-not $PackageOnly) {
        $process = Start-Process -FilePath $UnityPath -ArgumentList $buildArguments -PassThru -WindowStyle Hidden
        $process.WaitForExit()
        if ($process.ExitCode -ne 0) {
            $unityLog = Join-Path $logDirectory 'build-android.log'
            if (-not ([System.IO.File]::ReadAllText($unityLog).Contains('Unable to establish loopback connection'))) {
                throw ('Budowanie nie powiodło się. Sprawdź ' + $unityLog)
            }
        }
    }
    if ($PackageOnly -or $process.ExitCode -ne 0) {
        # Niektóre wersje Unity zmieniają środowisko Javy. Jeśli natywny kod już powstał,
        # spakuj bezpośrednio wygenerowany projekt Gradle.
        $androidTools = Join-Path (Split-Path $UnityPath) 'Data\PlaybackEngines\AndroidPlayer'
        $java = Join-Path $androidTools 'OpenJDK\bin\java.exe'
        $gradle = Get-ChildItem (Join-Path $androidTools 'Tools\gradle\lib') -Filter 'gradle-launcher-*.jar' | Select-Object -First 1
        $gradleProject = Join-Path $projectRoot 'Library\Bee\Android\Prj\IL2CPP\Gradle'
        if (-not (Test-Path -LiteralPath (Join-Path $gradleProject 'settings.gradle'))) {
            throw 'Brak wygenerowanego projektu. Najpierw uruchom pełne budowanie bez -PackageOnly.'
        }
        # Świeży katalog wynikowy omija blokady starych plików roboczych w OneDrive.
        $packageFolder = 'Temp/AndroidPackage-' + [Guid]::NewGuid().ToString('N')
        $packageRoot = $buildDrive + '/' + $packageFolder
        [System.IO.Directory]::CreateDirectory((Join-Path $projectRoot $packageFolder)) | Out-Null
        $initPath = $packageRoot + '/outputs.gradle'
        $initScript = "gradle.beforeProject { p -> p.layout.buildDirectory.set(new File('" + $packageRoot + "', p.name)) }"
        [System.IO.File]::WriteAllText($initPath, $initScript, [System.Text.UTF8Encoding]::new($false))
        # AGP zapisuje polecenia z pełnymi ścieżkami cache. Tymczasowy dysk utrzymuje
        # ścieżki ASCII, nawet gdy nazwa konta Windows zawiera polskie znaki.
        Push-Location -LiteralPath $gradleProject
        try {
            & $java '-Djdk.net.unixdomain.tmpdir=NUL' -classpath $gradle.FullName org.gradle.launcher.GradleMain '-p' ($buildDrive + '\Library\Bee\Android\Prj\IL2CPP\Gradle') '-g' ($buildDrive + '\Library\GradleCache') --init-script $initPath --no-daemon assembleRelease --stacktrace *> (Join-Path $logDirectory 'gradle-android.log')
            if ($LASTEXITCODE -ne 0) { throw ('Pakowanie APK nie powiodło się. Sprawdź ' + (Join-Path $logDirectory 'gradle-android.log')) }
            Copy-Item -LiteralPath ($packageRoot + '/launcher/outputs/apk/release/launcher-release.apk') -Destination (Join-Path $projectRoot 'Builds\Android\Pasjans.apk')
        } finally { Pop-Location }
    }
    Write-Output ('Gotowy APK: ' + (Join-Path $projectRoot 'Builds\Android\Pasjans.apk'))
} finally {
    $env:JAVA_TOOL_OPTIONS = $previousOptions
    $env:GRADLE_USER_HOME = $previousGradleHome
    if ($previousCodePage) { & $env:ComSpec /d /c chcp $previousCodePage | Out-Null }
    if ($buildDrive) { & subst $buildDrive /D }
}
