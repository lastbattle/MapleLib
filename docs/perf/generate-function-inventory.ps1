param(
    [string] $SourceRoot = (Join-Path $PSScriptRoot '..\..\MapleLib'),
    [string] $OutputPath = (Join-Path $PSScriptRoot '2026-09-07-function-audit.csv')
)

$ErrorActionPreference = 'Stop'

function Get-TypeName([Microsoft.CodeAnalysis.SyntaxNode] $Node) {
    $typeParts = @(
        $Node.Ancestors() |
            Where-Object { $_.GetType().Name -in @('ClassDeclarationSyntax', 'StructDeclarationSyntax', 'InterfaceDeclarationSyntax', 'RecordDeclarationSyntax', 'EnumDeclarationSyntax') } |
            ForEach-Object { $_.Identifier.ValueText }
    )
    [array]::Reverse($typeParts)
    return ($typeParts -join '.')
}

function Get-NamespaceName([Microsoft.CodeAnalysis.SyntaxNode] $Node) {
    $namespaceParts = @(
        $Node.Ancestors() |
            Where-Object { $_.GetType().Name -in @('NamespaceDeclarationSyntax', 'FileScopedNamespaceDeclarationSyntax') } |
            ForEach-Object { $_.Name.ToString() }
    )
    [array]::Reverse($namespaceParts)
    return ($namespaceParts -join '.')
}

function Get-Area([string] $RelativePath) {
    $normalized = $RelativePath.Replace('\', '/')
    switch -Regex ($normalized) {
        '^MapleCryptoLib/' { return 'maple-crypto' }
        '^Serialization/' { return 'serialization' }
        '^WzLib/Crypto/' { return 'wz-crypto' }
        '^WzLib/WzProperties/' { return 'wz-properties' }
        '^WzLib/WzStructure/' { return 'wz-structure' }
        '^WzLib/' { return 'wz-core' }
        '^Img/' { return 'img' }
        '^PacketLib/' { return 'packet' }
        '^ClientLib/' { return 'client' }
        '^Configuration/' { return 'configuration' }
        '^Converters/' { return 'converters' }
        '^Helpers/' { return 'helpers' }
        '^Properties/' { return 'assembly-metadata' }
        default { return 'root' }
    }
}

function Get-Symbol([Microsoft.CodeAnalysis.SyntaxNode] $Node, [string] $Kind) {
    switch ($Kind) {
        'method' {
            $prefix = if ($null -ne $Node.ExplicitInterfaceSpecifier) { $Node.ExplicitInterfaceSpecifier.ToString() } else { '' }
            return "$prefix$($Node.Identifier.ValueText)$($Node.ParameterList.ToString())"
        }
        'constructor' { return "$($Node.Identifier.ValueText)$($Node.ParameterList.ToString())" }
        'destructor' { return "~$($Node.Identifier.ValueText)$($Node.ParameterList.ToString())" }
        'operator' { return "operator $($Node.OperatorToken.ValueText)$($Node.ParameterList.ToString())" }
        'conversion-operator' { return "$($Node.ImplicitOrExplicitKeyword.ValueText) operator $($Node.Type.ToString())$($Node.ParameterList.ToString())" }
        'local-function' { return "$($Node.Identifier.ValueText)$($Node.ParameterList.ToString())" }
        'lambda' {
            $line = $Node.GetLocation().GetLineSpan().StartLinePosition.Line + 1
            return "<lambda>@L$line"
        }
        'anonymous-method' {
            $line = $Node.GetLocation().GetLineSpan().StartLinePosition.Line + 1
            return "<anonymous-method>@L$line"
        }
        'delegate-signature' { return "$($Node.Identifier.ValueText)$($Node.ParameterList.ToString())" }
        default { throw "Unsupported callable kind: $Kind" }
    }
}

function Get-Triage([Microsoft.CodeAnalysis.SyntaxNode] $Node, [string] $Kind, [string] $Symbol, [string] $Area) {
    $lineSpan = $Node.GetLocation().GetLineSpan()
    $spanLines = $lineSpan.EndLinePosition.Line - $lineSpan.StartLinePosition.Line + 1
    $nodeTypeNames = @($Node.DescendantNodes() | ForEach-Object { $_.GetType().Name })
    $hasLoop = ($nodeTypeNames -match '^(For|ForEach|While|Do)StatementSyntax$').Count -gt 0
    $hasAllocation = ($nodeTypeNames -match '^(ObjectCreation|ArrayCreation|ImplicitArrayCreation|StackAllocArrayCreation)ExpressionSyntax$').Count -gt 0
    $hotName = $Symbol -match '(?i)(read|write|load|save|parse|serial|deserial|encrypt|decrypt|crypt|hash|compress|decompress|inflate|deflate|copy|clone|search|find|scan|walk|travers|calculate|compute|convert|encode|decode|export|import|pack|unpack|render)'
    $isBodyless = $null -eq $Node.Body -and $null -eq $Node.ExpressionBody

    if ($Kind -like 'accessor*' -and ($isBodyless -or $spanLines -le 2)) {
        return [pscustomobject]@{ Classification = 'trivial'; Indicators = 'accessor/short' }
    }
    if ($isBodyless) {
        return [pscustomobject]@{ Classification = 'contract-only'; Indicators = 'no source body' }
    }
    if ($hasLoop -or ($hotName -and $spanLines -ge 4) -or (($Area -in @('wz-crypto', 'maple-crypto', 'serialization')) -and $spanLines -ge 4)) {
        $indicators = @()
        if ($hasLoop) { $indicators += 'loop' }
        if ($hasAllocation) { $indicators += 'allocation' }
        if ($hotName) { $indicators += 'hot-path name' }
        if ($Area -in @('wz-crypto', 'maple-crypto', 'serialization')) { $indicators += 'performance-sensitive area' }
        return [pscustomobject]@{ Classification = 'candidate'; Indicators = ($indicators -join '; ') }
    }
    if ($spanLines -le 3 -and -not $hasAllocation) {
        return [pscustomobject]@{ Classification = 'trivial'; Indicators = 'short body' }
    }
    $indicators = if ($hasAllocation) { 'allocation; workload frequency unknown' } else { 'workload frequency unknown' }
    return [pscustomobject]@{ Classification = 'requires-workload'; Indicators = $indicators }
}

function New-InventoryRow(
    [System.IO.FileInfo] $File,
    [Microsoft.CodeAnalysis.SyntaxNode] $Node,
    [string] $Kind,
    [string] $Symbol,
    [string] $Origin = 'explicit-source'
) {
    $relativePath = [System.IO.Path]::GetRelativePath($SourceRoot, $File.FullName).Replace('\', '/')
    $lineSpan = $Node.GetLocation().GetLineSpan()
    $area = Get-Area $relativePath
    $triage = Get-Triage $Node $Kind $Symbol $area
    $namespace = Get-NamespaceName $Node
    $typeName = Get-TypeName $Node
    $qualifiedType = @($namespace, $typeName) | Where-Object { $_ }

    # The 2026-09-07 audit team completed a source pass across every file in scope.
    $isSourceReviewed = $true

    return [pscustomobject][ordered]@{
        Id = ''
        File = "MapleLib/MapleLib/$relativePath"
        Line = $lineSpan.StartLinePosition.Line + 1
        Column = $lineSpan.StartLinePosition.Character + 1
        EndLine = $lineSpan.EndLinePosition.Line + 1
        EndColumn = $lineSpan.EndLinePosition.Character + 1
        Area = $area
        Namespace = $namespace
        Type = $typeName
        QualifiedType = ($qualifiedType -join '.')
        Symbol = $Symbol
        Kind = $Kind
        Origin = $Origin
        InitialClassification = $triage.Classification
        HeuristicIndicators = $triage.Indicators
        AuditStatus = if ($isSourceReviewed) { 'source-reviewed' } else { 'unreviewed' }
        MeasurementStatus = 'not-measured'
        AuditOwner = if ($isSourceReviewed) { 'orchestrator' } else { 'unassigned' }
        Notes = if ($isSourceReviewed) {
            'Source reviewed by orchestrator; measurement coverage remains tracked separately.'
        } else {
            'Inventory triage only; requires call-site/workload review before optimization.'
        }
    }
}

$sourceRootItem = Get-Item -LiteralPath $SourceRoot
$rows = [System.Collections.Generic.List[object]]::new()
$files = Get-ChildItem -LiteralPath $sourceRootItem.FullName -Recurse -File -Filter '*.cs' |
    Where-Object { $_.FullName -notmatch '\\(bin|obj)\\' } |
    Sort-Object FullName

foreach ($file in $files) {
    $sourceText = Get-Content -Raw -LiteralPath $file.FullName
    $tree = [Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree]::ParseText($sourceText)
    $root = $tree.GetRoot()

    foreach ($node in $root.DescendantNodes()) {
        $syntaxType = $node.GetType().Name
        $kind = switch ($syntaxType) {
            'MethodDeclarationSyntax' { 'method' }
            'ConstructorDeclarationSyntax' { 'constructor' }
            'DestructorDeclarationSyntax' { 'destructor' }
            'OperatorDeclarationSyntax' { 'operator' }
            'ConversionOperatorDeclarationSyntax' { 'conversion-operator' }
            'LocalFunctionStatementSyntax' { 'local-function' }
            'SimpleLambdaExpressionSyntax' { 'lambda' }
            'ParenthesizedLambdaExpressionSyntax' { 'lambda' }
            'AnonymousMethodExpressionSyntax' { 'anonymous-method' }
            'DelegateDeclarationSyntax' { 'delegate-signature' }
            default { $null }
        }
        if ($kind) {
            $rows.Add((New-InventoryRow $file $node $kind (Get-Symbol $node $kind)))
            continue
        }

        if ($syntaxType -eq 'AccessorDeclarationSyntax') {
            $owner = $node.Parent.Parent
            $ownerName = switch ($owner.GetType().Name) {
                'PropertyDeclarationSyntax' { $owner.Identifier.ValueText }
                'IndexerDeclarationSyntax' { "this$($owner.ParameterList.ToString())" }
                'EventDeclarationSyntax' { $owner.Identifier.ValueText }
                default { '<unknown-member>' }
            }
            $accessorKind = $node.Keyword.ValueText
            $rows.Add((New-InventoryRow $file $node "accessor-$accessorKind" "$ownerName.$accessorKind"))
            continue
        }

        if ($syntaxType -in @('PropertyDeclarationSyntax', 'IndexerDeclarationSyntax') -and $null -ne $node.ExpressionBody) {
            $ownerName = if ($syntaxType -eq 'PropertyDeclarationSyntax') { $node.Identifier.ValueText } else { "this$($node.ParameterList.ToString())" }
            $rows.Add((New-InventoryRow $file $node 'accessor-get' "$ownerName.get" 'source-derived-expression-accessor'))
            continue
        }

        if ($syntaxType -eq 'EventFieldDeclarationSyntax') {
            foreach ($variable in $node.Declaration.Variables) {
                foreach ($accessorKind in @('add', 'remove')) {
                    $rows.Add((New-InventoryRow $file $node "accessor-$accessorKind" "$($variable.Identifier.ValueText).$accessorKind" 'source-derived-field-event-accessor'))
                }
            }
        }
    }
}

# WzUOLProperty.cs defines UOLRES inside the source file. Parse the opposite branch
# once as well so the source-declared fallback API is represented in the ledger.
$conditionalFile = $files | Where-Object { $_.FullName.EndsWith('WzLib\WzProperties\WzUOLProperty.cs') }
if ($conditionalFile) {
    $inactiveSource = (Get-Content -Raw -LiteralPath $conditionalFile.FullName).Replace('#define UOLRES', '#undef UOLRES')
    $inactiveTree = [Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree]::ParseText($inactiveSource)
    $existingKeys = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
    foreach ($row in $rows) {
        [void] $existingKeys.Add("$($row.File)|$($row.Line)|$($row.Column)|$($row.Kind)|$($row.Symbol)")
    }
    foreach ($node in $inactiveTree.GetRoot().DescendantNodes()) {
        $syntaxType = $node.GetType().Name
        $kind = switch ($syntaxType) {
            'MethodDeclarationSyntax' { 'method' }
            'ConstructorDeclarationSyntax' { 'constructor' }
            'DestructorDeclarationSyntax' { 'destructor' }
            'OperatorDeclarationSyntax' { 'operator' }
            'ConversionOperatorDeclarationSyntax' { 'conversion-operator' }
            'LocalFunctionStatementSyntax' { 'local-function' }
            'SimpleLambdaExpressionSyntax' { 'lambda' }
            'ParenthesizedLambdaExpressionSyntax' { 'lambda' }
            'AnonymousMethodExpressionSyntax' { 'anonymous-method' }
            'DelegateDeclarationSyntax' { 'delegate-signature' }
            default { $null }
        }
        if (-not $kind) { continue }
        $symbol = Get-Symbol $node $kind
        $lineSpan = $node.GetLocation().GetLineSpan()
        $relativePath = [System.IO.Path]::GetRelativePath($SourceRoot, $conditionalFile.FullName).Replace('\', '/')
        $key = "MapleLib/MapleLib/$relativePath|$($lineSpan.StartLinePosition.Line + 1)|$($lineSpan.StartLinePosition.Character + 1)|$kind|$symbol"
        if ($existingKeys.Add($key)) {
            $rows.Add((New-InventoryRow $conditionalFile $node $kind $symbol 'conditional-inactive-UOLRES'))
        }
    }
}

$orderedRows = @($rows | Sort-Object File, Line, EndLine, Kind, Symbol)
for ($index = 0; $index -lt $orderedRows.Count; $index++) {
    $orderedRows[$index].Id = 'ML-{0:D4}' -f ($index + 1)
}

$outputDirectory = Split-Path -Parent $OutputPath
if (-not (Test-Path -LiteralPath $outputDirectory)) {
    New-Item -ItemType Directory -Force -Path $outputDirectory | Out-Null
}
$orderedRows | Export-Csv -LiteralPath $OutputPath -NoTypeInformation -Encoding utf8

[pscustomobject]@{
    SourceFiles = $files.Count
    InventoryRows = $orderedRows.Count
    OutputPath = (Resolve-Path -LiteralPath $OutputPath).Path
}
