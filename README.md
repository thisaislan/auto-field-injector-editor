<h1 style="font-size: 40px" align ="center">
  Auto Field Injector Editor

  <h4  align ="center">
    This Unity Editor tool lets you drag any GameObject, Component, ScriptableObject , or any Serializable class directly onto a custom script's Inspector header. It automatically adds a properly formatted [SerializeField] field to your script's source code, assigns the dropped reference, and handles missing using directives — all without ever opening the script file.
    <br>
  </h4>
</h1>

  <h4  align ="center">
    ✨ Drag, drop, and let the code write itself.
    <br>
  </h4>
</h1>

<p align="center">
    <a href="https://unity3d.com/get-unity/download">
        <img src="https://img.shields.io/badge/unity-tools-blue" alt="Unity Download Link"></a>
    <a href="https://github.com/thisaislan/auto-field-injector-editor/blob/main/LICENSE.md">
        <img src="https://img.shields.io/badge/License-MIT-brightgreen.svg" alt="License MIT"></a>
    <a href="https://chat.deepseek.com">
        <img src="https://img.shields.io/badge/%F0%9F%92%AC-DeepSeek%20AI-blue" alt="DeepSeek"></a>
</p>

---

### Table of Contents
- [Features](#-features)
- [How to Use](#-how-to-use)
- [Install](#-install)
- [Support](#-support)
- [Thanks](#-thanks)
- [License](#-license)

## ✨ Features

| Feature | Description |
|---------|-------------|
| **Drag‑and‑drop anywhere** | Drop any asset, GameObject, or component onto the drop zone — works with **GameObjects, Components, ScriptableObjects, Meshes, Textures, AudioClips, Materials**, or list of it and more. |
| **Smart component picker** | If you drop a GameObject, a window shows the GameObject itself and all its components with icons. Pick exactly what you want to reference. |
| **Auto‑generated field name** | Intelligent name suggestion based on the object's name and type, with **reserved‑word detection** (e.g., `transform` → `transformRef`). Follows **lower camelCase** convention. |
| **Visibility choice** | Choose between `[SerializeField] private` (default) or `public` fields. |
| **Automatic `using` directives** | Detects the namespace of the dropped type and adds missing `using` statements to the script (e.g., `TMPro`, `UnityEngine.UI`). |
| **Code formatting** | Fields are indented correctly, placed after existing serialized fields or constants, and **newlines are normalised** to keep your code clean. |
| **Pending changes list** | Add multiple fields at once; they appear in a pending list. Apply all changes with one click. |
| **Survives recompilation** | Assignments persist through Unity's domain reload using `EditorPrefs` – the reference is set after recompilation. |
| **Auto‑apply on inspector close** | If you forget to apply, changes are automatically committed when you close the inspector. |
| **Clean UI** | A compact drop zone and a clear pending list, with cancel/remove buttons. All windows close on **ESC** or click‑outside. |


## 🚀 How to Use

Want to add a new serialized reference to the code? Locate the following section in the script Inspector and drag it there

<div align="center" style="text-align:center;">
  <img src="https://github.com/thisaislan/just-images/raw/main/images/auto-field-injector-editor/drop-zone.png"  width="600" > 
</div>
<br>

Configure this

<div align="center" style="text-align:center;">
  <img src="https://github.com/thisaislan/just-images/raw/main/images/auto-field-injector-editor/window-single-ref.png"  width="400" > 
</div>
<br>

After adding the field, you can verify it or add more fields

<div align="center" style="text-align:center;">
  <img src="https://github.com/thisaislan/just-images/raw/main/images/auto-field-injector-editor/inspector.png"  width="400" > 
</div>
<br>


Maybe add a whole list

<div align="center" style="text-align:center;">
  <img src="https://github.com/thisaislan/just-images/raw/main/images/auto-field-injector-editor/window-mult-ref.png"  width="400" > 
</div>
<br>

When everything is perfect, simply apply, and that will be the result

<div align="center" style="text-align:center;">
  <img src="https://github.com/thisaislan/just-images/raw/main/images/auto-field-injector-editor/inspector-list.png"  width="520" > 
</div>
<br>



## 📦 Install

1. Copying git url https://github.com/thisaislan/auto-field-injector-editor.git

2. Click on `Window/Package Manager` in Unity Editor

3. Click on add package button `Add package button`

4. Select `Add package from git URL...`

5. Past the url

6. Press `Enter` or clink on the `Add` button

7. Enjoy :satisfied:

</br>

## 🤝 Support
Please submit any queries, bugs or issues, to the [Issues](https://github.com/thisaislan/auto-field-injector-editor/issues) page on this repository. All feedback is appreciated as it not just helps myself find problems I didn't otherwise see, but also helps improve the project.

</br>

## 💖 Thanks
My friends and family, and you for having come here!

</br>

## 📄 License
Copyright (c) 2026-present Aislan Tavares (@thisaislan) and Contributors. This is free and open-source software licensed under the [MIT License](https://github.com/thisaislan/auto-field-injector-editor/blob/main/LICENSE.md).


<!--
  ko-fi donation button 
 -->
<br>
<br>
<br>
<br>
<br>
<br>
<h4 align="center" style="text-align:center;">
  <a href="https://ko-fi.com/thisaislan">
    <img src="https://github.com/thisaislan/just-images/raw/main/images/ko-fi/ko-fi_donation_banner.gif" style="width: 460px">
  </a>
</h4>

<h4 align="center" style="text-align:center;">
  Enjoy! ♥️
</h4>
<br>
