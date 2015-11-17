param(
    $buildType = "Release"
)


function clean{
    # CLEAN
    write-host "Cleaning" -foregroundcolor:blue
    if(!(Test-Path "$basePath\BuildOutput\"))
    {
        mkdir "$basePath\BuildOutput\"
    }
    if(!(Test-Path "$logPath"))
    {
        mkdir "$logPath"
    }
    if(!(Test-Path "$basePath\TestOutput\"))
    {
        mkdir "$basePath\TestOutput\"
    }    
    if(!(Test-Path "$basePath\releases\"))
    {
        mkdir "$basePath\releases\"
    }    
    remove-item $basePath\BuildOutput\* -recurse
    remove-item $basePath\TestOutput\* -recurse    
    remove-item $logPath\* -recurse
    
    $lastResult = $true
}

function preBuild{
   
    write-host "PreBuild"  -foregroundcolor:blue
    Try{
        # Set assembly VERSION
        Get-Content $basePath\src\$projectName\Properties\AssemblyInfo.Template.txt  -ErrorAction stop |
        Foreach-Object {$_ -replace "//{VERSION}", "[assembly: AssemblyVersion(""$buildVersion"")]"} | 
        Foreach-Object {$_ -replace "//{FILEVERSION}", "[assembly: AssemblyFileVersion(""$buildVersion"")]"} | 
        Set-Content $basePath\src\$projectName\Properties\AssemblyInfo.cs         

        # Set NUSPEC VERSION
        Get-Content $basePath\templates\$projectName.nuspec.template  -ErrorAction stop |
        Foreach-Object {$_ -replace "{VERSION}", "$buildVersion"} |         
        Set-Content $basePath\$projectName.nuspec   
    }
    Catch{
        Write-host "PREBUILD FAILED!" -foregroundcolor:red
        exit
    }
}

function build{

    preBuild

    # BUILD
    write-host "Building"  -foregroundcolor:blue
    $msbuild = "c:\Windows\Microsoft.NET\Framework\v4.0.30319\msbuild.exe"
    $solutionPath = "$basePath\src\$projectName.sln"
    Invoke-expression "$msbuild $solutionPath /p:configuration=$buildType /t:Clean /t:Build /verbosity:q /nologo > $logPath\LogBuild.log"

    if($? -eq $False){
        Write-host "BUILD FAILED!"
        exit
    }
    
    $content = (Get-Content -Path "$logPath\LogBuild.log")
    $failedContent = ($content -match "error")
    $failedCount = $failedContent.Count
    if($failedCount -gt 0)
    {    
        Write-host "BUILDING FAILED!" -foregroundcolor:red
        $lastResult = $false
        
        Foreach ($line in $content) 
        {
            write-host $line -foregroundcolor:red
        }
    }

    if($lastResult -eq $False){    
        exit
    } 
}

function nugetPublish{
    # DEPLOYING
    write-host "Publishing Nuget package" -foregroundcolor:blue
    $outputName = ".\releases\$projectName.$fullBuildVersion.nupkg"
    nuget push $outputName
}

function nugetPack{
    # Packing
    write-host "Packing" -foregroundcolor:blue
    nuget pack .\$projectName.nuspec -OutputDirectory .\releases > $logPath\LogPacking.log     
    if($? -eq $False){
        Write-host "PACK FAILED!"  -foregroundcolor:red
        exit
    }
}

function zipOutput{
    # ZIPPING
    write-host "Zipping" -foregroundcolor:blue
    $outputName = $projectName+"_V"+$buildVersion+"_BUILD.zip"
    zip a -tzip .\releases\$outputName -r .\BuildOutput\*.* > $logPath\LogZipping.log    
    if($? -eq $False){
        Write-host "Zipping FAILED!"  -foregroundcolor:red
        exit
    }
}

$basePath = Get-Location
$logPath = "$basePath\logs"
$buildVersion = Get-Content .\VERSION
$fullBuildVersion = "$buildVersion.0"
$projectName = "logsort"

if($buildType -eq "clean"){    
    clean  
    exit
}

if($buildType -eq "publish"){
    $buildType = "Release"

    clean
    build     
    zipOutput
    nugetPack 
    nugetPublish  

    exit
}

if($buildType -eq "debug"){
    $buildType="Debug"
}

    clean
    build    

Write-Host Finished -foregroundcolor:blue

