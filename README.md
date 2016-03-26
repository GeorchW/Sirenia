# Sirenia
A simple backup tool for personal use

# Build
Build it like you would do with any MSBuild (Visual Studio) project, there's nothing special with it :-). Although not tested, it should work on Linux/Mac just as well as on Windows (of course you would need Mono).

# Usage
You start the application and choose the path where the program should look for files. Folders can contain ignore.txt files, that work pretty much like gits .gitignore files. If there are multiple ignore.txt files at different levels of the directory tree, all of them are applied. When you like the selection of files shown to you in the list, you can copy all those files to a different location. That's it!

# ignore.txt examples
A file looking like this

    *
    
will ignore the entire folder it lies in. (Very handy!)

This

    *.bin
    
will ignore any files ending in .bin.

This

    SomeFileOrFolder
    
will ignore the file or folder named (exactly) SomeFileOrFolder.

This

    *.bin
    SomeFileOrFolder
    
combines both.
This

    @contains:.git
will ignore every subfolder that contains a folder named .git.
