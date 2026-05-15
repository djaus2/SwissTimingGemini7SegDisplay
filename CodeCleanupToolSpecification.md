# Code Cleanup Tool Specification

## Overview
This tool identifies and removes unused code elements and commented-out code from C# and XAML projects in a safe, automated manner.

## Supported Languages
- C# (.cs files)
- XAML (.xaml files)

## Analysis Actions

### 1. Unused Methods Detection
**Target:** C# private methods

**Methodology:**
1. Parse all C# files to identify all method declarations
2. For each method:
   - Search the entire codebase for method name references
   - Check if the method is assigned to delegates or event handlers
   - Check if the method is referenced via reflection (string-based calls)
   - Check if the method has special attributes (e.g., `[DllImport]`, `[Command]`)
3. Flag methods with zero references as candidates for removal

**Exclusions:**
- Methods with special attributes that may be called via reflection
- Methods assigned to event handlers in XAML
- Methods with `[DllImport]` for P/Invoke calls
- Methods with `[Obsolete]` attributes (warn instead)

### 2. Unused Fields Detection
**Target:** C# private fields

**Methodology:**
1. Parse all C# files to identify all field declarations
2. For each field:
   - Search the entire codebase for field name references
   - Check if the field is accessed via properties
   - Check if the field is serialized (has `[Serializable]`, `[DataMember]`, etc.)
   - Check if the field is used in data binding (WPF/MAUI)
3. Flag fields with zero references as candidates for removal

**Exclusions:**
- Fields with serialization attributes
- Fields used in property setters that reference the backing field
- Fields with `[DllImport]` or other special attributes

### 3. Unused Properties Detection
**Target:** C# private properties

**Methodology:**
1. Parse all C# files to identify all property declarations
2. For each property:
   - Search the entire codebase for property name references
   - Check if the property is used in data binding (XAML binding syntax)
   - Check if the property is used in MVVM command binding
   - Check if the property is referenced via reflection
3. Flag properties with zero references as candidates for removal

**Exclusions:**
- Properties with `[ObservableProperty]` (CommunityToolkit.Mvvm)
- Properties used in XAML data binding
- Properties with special attributes

### 4. Unused Variables Detection
**Target:** Local variables within methods

**Methodology:**
1. Parse each method to identify local variable declarations
2. For each variable:
   - Check if the variable is read after being written
   - Check if the variable is passed to other methods
   - Check if the variable is used in expressions
3. Flag variables that are written but never read as candidates for removal

**Exclusions:**
- Variables used only for their side effects (e.g., `Task` result ignored)
- Variables used in `using` statements
- Variables in `out` parameters

### 5. Commented-Out Code Detection

#### C# Files
**Target:** Multi-line comment blocks (`/* */`)

**Methodology:**
1. Find all `/* ... */` comment blocks
2. Analyze the content:
   - Check for C# syntax patterns (keywords, braces, semicolons)
   - Check for method/field declarations
   - Check for control structures (if, for, while, switch)
3. Flag comment blocks containing code-like patterns as candidates for removal

**Exclusions:**
- Single-line comments (`//`) - these are typically documentation
- Comment blocks containing only text descriptions
- Comment blocks with XML documentation tags (`<summary>`, `<param>`, etc.)

#### XAML Files
**Target:** XAML comment blocks (`<!-- -->`)

**Methodology:**
1. Find all `<!-- ... -->` comment blocks
2. Analyze the content:
   - Check for XAML element tags (`<`, `>`)
   - Check for attribute assignments (`=`)
   - Check for markup extensions (`{Binding ...}`)
3. Flag comment blocks containing XAML markup as candidates for removal

**Exclusions:**
- Comment blocks containing only text descriptions
- Comment blocks with documentation-like content

## Removal Actions

### Preview Mode
Before any removal:
1. Display a list of all candidates for removal
2. Group by file and type (method, field, property, variable, comment)
3. Show the code snippet that would be removed
4. Allow user to select/deselect individual items
5. Estimate impact (lines of code reduction)

### Safe Removal Process
1. **Backup:** Create a backup of the file before modification
2. **Atomic Changes:** Apply changes one at a time
3. **Validation:** After each change:
   - Verify the file still compiles (for C#)
   - Verify XAML syntax is valid (for XAML)
   - If compilation fails, revert the change and warn the user
4. **Report:** Generate a summary of changes made

### Post-Removal Formatting
- Remove extra blank lines left by removed code
- Fix indentation issues
- Ensure proper brace alignment
- Preserve existing code style

## Configuration

### File Patterns
- Include: `*.cs`, `*.xaml`
- Exclude: `*.Designer.cs`, `*.g.cs`, `*.g.i.cs` (generated files)

### Directories
- Exclude: `bin/`, `obj/`, `node_modules/`, `.git/`

### Safe Mode Options
- Skip files with `[GeneratedCode]` attribute
- Skip test files (optional)
- Skip files with specific keywords in path (configurable)

## Command Line Interface

```bash
# Analyze only (preview mode)
code-cleanup analyze --path ./src

# Remove unused code with preview
code-cleanup cleanup --path ./src --preview

# Remove commented-out code only
code-cleanup cleanup --path ./src --comments-only

# Dry run (show what would be removed)
code-cleanup cleanup --path ./src --dry-run

# Specific file types
code-cleanup cleanup --path ./src --include "*.cs" --exclude "*.Designer.cs"
```

## VS Code Extension Commands

- `CodeCleanup: Analyze Current File` - Show unused elements in current file
- `CodeCleanup: Analyze Workspace` - Show unused elements in entire workspace
- `CodeCleanup: Cleanup Current File` - Remove unused elements in current file
- `CodeCleanup: Cleanup Workspace` - Remove unused elements in entire workspace
- `CodeCleanup: Remove Commented Code` - Remove commented-out code blocks

## Visual Studio Extension Commands

- `Tools > Code Cleanup > Analyze Solution`
- `Tools > Code Cleanup > Analyze Project`
- `Tools > Code Cleanup > Cleanup Solution`
- `Tools > Code Cleanup > Cleanup Project`
- `Tools > Code Cleanup > Remove Commented Code`

## Output Format

### Console Output
```
Analyzing c:\Project\MainViewModel.cs...
  Found 0 unused methods
  Found 0 unused fields
  Found 0 unused properties
  Found 0 unused variables
  Found 0 commented-out code blocks

Analyzing c:\Project\WindGaugeWindow.xaml.cs...
  Found 2 unused methods:
    - UpdateTimeInputFromLapTime (line 395)
    - ComboBox_SelectionChanged (line 825)
  Found 2 unused fields:
    - _firstLapCounted (line 45)
    - _skipNextTimerUpdate (line 52)

Total candidates for removal: 4 methods, 2 fields, 0 properties, 0 variables, 0 comments
Estimated lines of code reduction: 45
```

### JSON Output (for CI/CD)
```json
{
  "summary": {
    "filesAnalyzed": 15,
    "unusedMethods": 2,
    "unusedFields": 2,
    "unusedProperties": 0,
    "unusedVariables": 0,
    "commentedCodeBlocks": 0,
    "linesReduced": 45
  },
  "changes": [
    {
      "file": "WindGaugeWindow.xaml.cs",
      "type": "method",
      "name": "UpdateTimeInputFromLapTime",
      "line": 395,
      "lines": 30
    }
  ]
}
```

## Safety Considerations

1. **Never remove code automatically** without user confirmation
2. **Always create backups** before modification
3. **Validate compilation** after each change
4. **Respect version control** - warn if files have uncommitted changes
5. **Handle special cases**:
   - Reflection-based calls
   - Serialization
   - Data binding (WPF/MAUI)
   - Event handlers in XAML
   - Test frameworks (xUnit, NUnit, MSTest)
