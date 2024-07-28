# Merlin - File Management Utility

Merlin is a versatile command-line utility for managing and manipulating files. It provides functionality to merge multiple files into a single file and extract files from a merged file back into separate files.

## Features

- Merge multiple files into a single file
- Extract files from a merged file
- Include/exclude files based on patterns
- Register/unregister the application in the system PATH
- Customizable file encoding support

## Installation

1. Download the latest release from the [releases page](https://github.com/yourusername/merlin/releases).
2. Extract the zip file to a directory of your choice.
3. (Optional) Run `merlin register` to add the application to your system PATH.

## Usage

Merlin supports the following commands:

### Merge Files

```
merlin merge -s <source_directory> -o <output_file> [-p <patterns>] [-e <encoding>]
```

- `-s, --source`: Source directory containing the files to merge (required)
- `-o, --output`: Output file path for the merged content (required)
- `-p, --patterns`: File patterns to include/exclude (e.g., `*.cs`, `!*Test.cs`)
- `-e, --encoding`: Encoding to use for reading/writing files (default: utf-8)

### Extract Files

```
merlin extract -i <input_file> -o <output_directory> [-e <encoding>]
```

- `-i, --input`: Input file to extract (required)
- `-o, --output`: Output directory for extracted files (required)
- `-e, --encoding`: Encoding to use for reading/writing files (default: utf-8)

### Register/Unregister

```
merlin register [-u]
```

- `-u, --unregister`: Unregister the application from the system PATH

## Examples

1. Merge all .cs files in the current directory, excluding test files:
   ```
   merlin merge -s . -o merged.txt -p *.cs !*Test.cs
   ```

2. Extract files from a merged file:
   ```
   merlin extract -i merged.txt -o extracted_files
   ```

3. Register Merlin in the system PATH:
   ```
   merlin register
   ```

## Building from Source

1. Ensure you have .NET 8.0 SDK installed.
2. Clone the repository: `git clone https://github.com/yourusername/merlin.git`
3. Navigate to the project directory: `cd merlin`
4. Build the project: `dotnet build -c Release`
5. Run the application: `dotnet run --project Merlin/Merlin.csproj`

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

Have Goat time 🐐