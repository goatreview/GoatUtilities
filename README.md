# Merlin File Manager

Merlin is a powerful utility for merging and extracting files, designed to simplify file management tasks in software development projects.

## Features

- Merge multiple files into a single file
- Extract content from a merged file into separate files
- Use GitIgnore-style patterns to include or exclude files
- Register the application in the system PATH for easy access

## Installation

1. Download the latest release from the [releases page](https://github.com/yourusername/merlin/releases).
2. Extract the downloaded archive to a directory of your choice.
3. (Optional) Run `merlin register` to add the application to your system PATH.

## Usage

### Merging Files

To merge files, use the `merge` command:

```
merlin merge -s <source_directory> -o <output_file> [-p <patterns>] [-e <encoding>]
```

- `-s, --source`: Source directory containing the files to merge (required)
- `-o, --output`: Output file path for the merged content (required)
- `-p, --patterns`: File patterns to include/exclude (optional)
- `-e, --encoding`: Encoding to use for reading/writing files (optional, default: utf-8)

#### Example: Merging C# files excluding obj and bin directories

```
merlin merge -s ./project -o merged_code.miz -p *.cs !obj/ !bin/
```

This command will:
1. Look for all `.cs` files in the `./project` directory and its subdirectories.
2. Exclude any files in `obj/` and `bin/` directories.
3. Merge the matching files into `merged_code.miz`.

### Extracting Files

To extract files from a merged file, use the `extract` command:

```
merlin extract -i <input_file> -o <output_directory> [-e <encoding>]
```

- `-i, --input`: Input file to extract (required)
- `-o, --output`: Output directory for extracted files (required)
- `-e, --encoding`: Encoding to use for reading/writing files (optional, default: utf-8)

#### Example: Extracting files from a merged file

```
merlin extract -i merged_code.miz -o ./extracted_project
```

This command will extract all files from `merged_code.miz` into the `./extracted_project` directory.

### Registering the Application

To add Merlin to your system PATH:

```
merlin register
```

To remove Merlin from your system PATH:

```
merlin register -u
```

## File Patterns

Merlin supports GitIgnore-style patterns for including and excluding files:

- Use `*` to match any number of characters within a filename or directory
- Use `**` to match any number of directories
- Prefix a pattern with `!` to negate it (exclude matching files)

Examples:
- `*.cs`: Include all C# files
- `!obj/`: Exclude the `obj` directory and its contents
- `**/*.txt`: Include all .txt files in any subdirectory

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

Have Goat time 🐐