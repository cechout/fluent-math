<img width="2560" height="810" alt="frame2" src="https://github.com/user-attachments/assets/ea524baf-ce68-42b2-8db9-5287a9ef363e" />

###

Simple Calculator is a native Windows 11 application built with C#, WinUI 3, and the MVVM pattern. Its goal is to provide an alternative to the default Windows Calculator. While the default app evaluates inputs step-by-step, this app works like a real physical calculator: you type the entire equation first and press `=` to calculate the final result.


## 📖 Project History & Architecture
The first version of this project [`v1.0.0`](https://github.com/cechout/simple-calculator/releases/tag/v1.0.0) was written in WPF. For version [`v2.0.0`](https://github.com/cechout/simple-calculator/releases/tag/v2.0.0), the UI framework and the code structure were changed:
* **WinUI 3:** Replaced WPF controls with Windows App SDK components (like `NavigationView`).
* **MVVM:** Separated the mathematical logic from the user interface. The code is divided into Models, Views, and ViewModels. They communicate via data binding and commands.


## ⚙️ Core Mechanics
### Expression Input in Standard Mode
The default Windows Calculator calculates a result after every operator. This app works like a real physical calculator (like a Casio). You type the whole equation exactly as you write it on paper (e.g., `(5 + 3) * 8 / 2`). It calculates everything at once when you press `=`. This guarantees the correct mathematical order of operations.

### Currency Converter
The app downloads daily exchange rates as an XML file from the European Central Bank (ECB). It uses an `XmlReader` to parse the data directly into a C# dictionary to calculate currency conversions. The base currency is the Euro (EUR).


## 🛠️ How to Run

### 1. Prerequisites
To build and run this project, it is highly recommended to use **Visual Studio 2022** (Version 17.0 or later). 
Before opening the solution, make sure you have the following workloads installed via the **Visual Studio Installer**:

* **.NET Desktop Development**
* **Windows application development** (Make that the "Windows App SDK C# Templates" are checked in the optional components on the right side).

### 2. Clone the Repository
```ps
git clone https://github.com/cechout/simple-calculator.git
```

### 3. Build and Run
* Open the solution file in Visual Studio.
* Right-click on `Calculator_WinUI` in the Solution Explorer and select Set as `Startup Project`.
* In the top toolbar, change the Solution Platform from `Any CPU` to your specific system architecture (e.g., `x64`). *Note: WinUI 3 projects do not support 'Any CPU' builds.*
* Press `F5` to build and run the application.

And now you're good to go!
