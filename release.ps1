# Create a new Release, auto incremented
param(
    [string]$version
)

$versionFile = ".version"

# ✅ Function: Increment patch version
function Increment-Version($ver) {
    if ($ver -match "^v(\d+)\.(\d+)\.(\d+)$") {
        $major = [int]$Matches[1]
        $minor = [int]$Matches[2]
        $patch = [int]$Matches[3]

        $patch++

        return "v$major.$minor.$patch"
    } else {
        Write-Error "Invalid version format. Expected vX.Y.Z"
        exit 1
    }
}

# ✅ Step 1: Determine version
if ($version) {
    Write-Host "Using provided version: $version"
}
else {
    if (Test-Path $versionFile) {
        $lastVersion = (Get-Content $versionFile -Raw).Trim()
        Write-Host "Last version found: $lastVersion"

        $version = Increment-Version $lastVersion
        Write-Host "Auto-incremented version: $version"
    }
    else {
        $version = "v1.0.0"
        Write-Host "No version file found. Starting at $version"
    }
}

# ✅ Step 2..4: Create a unique tag and push it
# If the tag exists locally or on the remote, auto-increment and retry.
$maxAttempts = 20
$attempt = 0
$remoteName = "origin"

while ($attempt -lt $maxAttempts) {
    $attempt++

    # ensure local tag doesn't already exist
    $localExisting = git tag --list $version
    if ($localExisting) {
        Write-Host "Local tag $version already exists — incrementing..."
        $version = Increment-Version $version
        continue
    }

    # check remote for existing tag
    $remoteExisting = git ls-remote --tags $remoteName "refs/tags/$version" 2>$null
    if ($remoteExisting) {
        Write-Host "Remote already has tag $version — auto-incrementing..."
        $version = Increment-Version $version
        continue
    }

    # create local tag
    Write-Host "Creating tag $version..."
    git tag $version

    # attempt to push
    Write-Host "Pushing tag to $remoteName..."
    git push $remoteName $version
    $exit = $LASTEXITCODE

    if ($exit -eq 0) {
        Write-Host "✅ Release triggered for $version"
        # persist version only after successful push
        Set-Content $versionFile $version
        exit 0
    }

    # push failed — remove local tag and retry
    Write-Warning "Failed to push tag $version (exit code $exit). Removing local tag and retrying..."
    git tag -d $version 2>$null

    # if remote now has the tag (race), increment and retry
    $remoteExistingNow = git ls-remote --tags $remoteName "refs/tags/$version" 2>$null
    if ($remoteExistingNow) {
        $version = Increment-Version $version
        continue
    }

    # otherwise give up with error
    Write-Error "Failed to push tag $version and remote does not report the tag. Aborting."
    exit 1
}

Write-Error "Exceeded maximum attempts ($maxAttempts) trying to find a unique version tag. Aborting."
exit 1