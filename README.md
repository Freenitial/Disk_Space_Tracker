# Disk Space Tracker

A compact Windows GUI to **track free disk space in real time**, show deltas (added / deleted), copy values in your preferred unit, and even **automate actions** based on flexible conditions (files, processes, registry, timers, text files).

---

## ✨ Features

* 🕒 **Live tracking** of free space on any local drive (C:, D:, …) with a running timer.
* 🔻🔺 **Delta view**: current difference, **Diff Added** and **Diff Deleted** since start.
* 📎 **One-click copy** in **Bytes / MB / GB** (optional “Copy unit” suffix).
* 📈 **Disk speeds** (MB/s): read & write via performance counters (when available).
* 🧰 **Automation mode** with triggers (**Start / Pause / Resume / Reset**) under conditions.
* 💼 **Presets**: save/load complete automation configurations from JSON.
* 🧾 **Logs & Report**: sidebar log grid + report window you can copy.

---

## 📷 ScreenShots 

**Basic mode**:

<img width="535" height="275" alt="image" src="https://github.com/user-attachments/assets/94d45fb6-01f5-41e1-af5e-90c51b5a36d1" />

**Auto mode**:

<img width="835" height="513" alt="image" src="https://github.com/user-attachments/assets/94307a6f-4202-40d5-b228-1614d2d163f1" />

---

## 🤖 Automation mode (Auto ▿)

Automation lets you fire an app action when its **conditions** are met. 

Each **Trigger** below can be armed and validated by **All** or **Any** of its conditions:

### Triggers

* **Start (if reset)** ▶️ starts tracking
* **Pause (if started)** ⏸️ pauses
* **Resume (if paused)** ▶️ resumes
* **Reset** 🔄 stops, clears peaks, re-reads current free space

### Conditions (per trigger)

* ⌛ **Wait time**: count down HH:MM:SS before the trigger can fire (shows an inline countdown).
* ▶️ **Execute, then wait closing**: runs an EXE/BAT/CMD/PS1/MSI or raw command (cmd /c ...).
* 📄 **File exist**: path (supports wildcards).
* 🔒 **File locked**: path (supports wildcards).
* 🧠 **Process exist**: by name (wildcards allowed) or PID.
* 🧱 **Registry entry**: hive/key with optional **Value** and/or **Data** (exact or wildcard).
* 📝 **Text file**: select line(s) by number/range/negative index (`-1` last line, `5:10`, `any`) and compare:

  * `=` equal, `!=` not equal, `*` contains, `!*` not contains, `starts`, `ends` (case-insensitive).
  * Optimized tail reader for UTF-8/ANSI when targeting last lines.

### After-actions

* 🔁 **Loop**: re-arm trigger automatically (good for periodic checks).
* 🔔 **Beep**: play a notification sound.
* ♻️ **Restart (Reset only)**: auto-start tracking again right after Reset.

---

## 💾 Presets

* Click the **“Presets”** dropdown (top-right in Auto).
* **Save as…** stores the full Auto state (triggers, conditions, After-actions) as JSON in
  `presets.json` next to the executable.
* **Apply** a preset to instantly restore your automation layout.
* **Delete** a preset by clicking the small **X** zone on the right edge of a dropdown item.

---

## 📦 Installation

No installation required — it is a single self-contained file (no .NET runtime needed).

**➡️ [Download Disk_Space_Tracker.exe](https://github.com/Freenitial/Disk_Space_Tracker/releases/latest/download/Disk_Space_Tracker.exe)**

Then just double-click to open.

---

## 🔧 Build from source (optional)

Most users don’t need this — just download the executable above. If you’d rather compile it
yourself, see **[BUILD.md](https://github.com/Freenitial/Disk_Space_Tracker/blob/main/BUILD.md)**.

---

## 🚀 Quick start

1. Launch the app and pick your **drive** (e.g., `C:`).
2. Click **Start**. Watch **Current free** and **Current difference** update each second.
3. Use **Copy** buttons to copy deltas in your preferred unit (toggle **Copy unit** to append “MB/GB”).
4. Click **Report** anytime to view a formatted summary.
5. Explore **Auto ▿** to arm triggers, add conditions, and save a **Preset**.

---

## 🧰 Requirements

* Windows 10/11
* For disk speed: **Performance Counters** (LogicalDisk) must be enabled.

---

## ⚠️ Notes & Limitations

* The **Max** checkboxes next to “Added” and “Deleted” let you track peaks while **ignoring** movement in the opposite direction.
* Registry checks compare **strings** (byte arrays shown as hex, multi-strings joined by commas).
