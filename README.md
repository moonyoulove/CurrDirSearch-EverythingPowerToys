# Current Directory Search

## Usage
Automatically locates the top-level Windows Explorer for the current directory, retrieves the folder path, and replaces the "." with the path. For example:

    . SomePicture.jpg => "C:\Some\Path\" SomePicture.jpg

Using "." followed by a file or folder name will add "parent:" to the search query, indicating not to search within subdirectories. After selecting and confirming the search result, it will automatically navigate to the corresponding file or folder. For example:

    .\ SomePicture.jpg => parent:"C:\Some\Path\" SomePicture.jpg

## Compile
The main modification is done in "Everything.cs." To compile, follow these steps:

1. Clone the PowerToys repository.
2. Add this project to the plugins folder.