# 🔒 Privacy

Fluent Math is a calculator and a unit converter that runs on your own machine. There is no account, no sign-in, no telemetry and no analytics. Your settings and your calculations stay on your machine.

The app does go online for two things: the exchange rates of the currency converter, and updates. This page explains when that happens, where the request goes and what is sent.

## 📁 What is stored, and where

Everything the app remembers is written to your machine as plain JSON files that you can open, copy or delete:

* **Installer build:** `%LocalAppData%\FluentMath`
* **Portable build:** a `Persistence` folder next to `FluentMath.exe`
* **Microsoft Store build:** the packages own `LocalState` folder under `%LocalAppData%\Packages`, which Windows deletes together with the app when you uninstall it

The app shows you the exact folder it is using: **Settings**, **Backup & Reset**, **App Data Folder**.

These files hold your settings, your window sizes and positions, and the units each converter page last showed. Beside them the app keeps the last exchange rates it loaded, so the currency converter also works offline, a `cache` folder for the release notes it has already loaded, and a `quarantine` folder for files that could not be read any more. All of it can be deleted at any time, and none of it is ever uploaded.

## 🌐 When the app goes online

The installer and portable builds contact only the European Central Bank and GitHub, and only in these cases. The Store build differs slightly, see [Microsoft Store version](#-microsoft-store-version).

* **When the currency converter opens**, and when you refresh its rates, to download the daily reference rates from `www.ecb.europa.eu`. This is the public rates file the ECB publishes for everyone.
* **On startup**, to ask `api.github.com` whether a newer release exists. This can be turned off, see below.
* **When you open "What's New"**, to load the list of published versions from `api.github.com`. They are saved on your machine afterwards, so this only happens when a version is missing from that copy.
* **When you confirm an update**, to download the new build from the releases page on `github.com`. Nothing is downloaded before you press the update button.

Each of these is a plain read request. The app sends nothing along with it: no version number, no machine name, no settings, no calculations, nothing that names you.

The ECB and GitHub do see the same things every website sees when you visit it: your IP address, the time and which address was asked for. GitHub also sees the name "FluentMath" as the user agent. What they do with that is covered by the [ECB privacy statement](https://www.ecb.europa.eu/services/data-protection/privacy-statements/html/index.en.html) and the [GitHub Privacy Statement](https://docs.github.com/en/site-policy/privacy-policies/github-privacy-statement).

Please note that an IP address counts as personal data in the EU. That is why this page exists, even though the app itself collects nothing.

## ⚙️ How to keep the app offline

Open **Settings**, expand **Fluent Math** under **About & Open Source**, and turn off **Check for Updates on Startup**. With that switch off the app only goes online when you ask it to.

These still work, because you ask for them yourself:

* The currency converter loads the rates when you open it or refresh it. Without a connection it uses the last rates it loaded.
* The update card in the same expander still checks when you select it.
* Opening "What's New" loads the published versions, but only when the copy saved on your machine is missing one, and at most once per app start.
* An update is still downloaded and installed when you confirm it.

## 🏪 Microsoft Store version

The Store version asks the Microsoft Store whether an update exists, in the same situations and behind the same switch as above. It still reads the latest release from `api.github.com`, but only to name the new version. When you confirm an update, the Store downloads and installs it; nothing comes from GitHub.

The Store check runs through the Store service built into Windows, which knows which version is installed, the same as for every Store app. What Microsoft does with that is covered by the [Microsoft Privacy Statement](https://privacy.microsoft.com/privacystatement). The Store can also update the app in the background on its own, as it does for all Store apps; that is a setting of the Microsoft Store, not of Fluent Math.
