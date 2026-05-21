
code = '''using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage.Pickers;
using Windows.Storage;
using WinRT.Interop;
using Todo.Models;
'''
with open(r"D:\Project\Todo\MainWindow.Notepad.cs", "w", encoding="utf-8", newline="") as f:
    f.write(code)
