# LoGiViT — Lower Gastrointestinal Virtual Trainer

**LoGiViT** is an advanced virtual reality (VR) training environment for colonoscopy skill acquisition, built in **Unity 6** with the **High Definition Render Pipeline (HDRP)**. It combines a procedural, parameterized authoring system for 3D large intestine models with dedicated training modules addressing systematic mucosal coverage and polyp identification.

The project is developed as part of a doctoral dissertation on the modeling and simulation of colonoscopy using virtual reality at the **Universidade de Vigo**.

---

> 🌐 **Official Website:** [https://logivit.atlanttic.uvigo.es/](https://logivit.atlanttic.uvigo.es/)  
> 📖 **Comprehensive Technical Documentation:**  
> For in-depth architectural details, directory and script breakdowns, clinical scoring formulas, rendering pipelines, hardware protocols, and developer guides, please refer to [**OVERVIEW_ENG.md**](OVERVIEW_ENG.md) (or [**OVERVIEW.md**](OVERVIEW.md) for Spanish).

---

## Key Features

- **Procedural Anatomical Authoring (Model Editor / LIME):**
  - Generates anatomical colon models along 3D splines with customizable anatomical loops (*Alpha, Reversed Alpha, Gamma, N, and Transverse loops*).
  - Parametric tissue deformation, haustral folds, and lumen calibration via blendshapes and Perlin noise.
  - Interactive placement and geometric welding of pathological lesions (polyps).
- **Mucosal Coverage Training:**
  - Real-time tracking of inspected intestinal wall surface using a circular perceptual cone and parallelized occlusion testing (C# Jobs & Burst Compiler).
  - Time-based observation heatmaps and objective, section-by-section scoring.
- **Polyp Detection & Diagnostic Training:**
  - Interactive lesion detection and standardized clinical classification:
    - **Paris Classification:** Morphological evaluation (`0-Ip`, `0-Is`, `0-IIa`, `0-IIb`, `0-IIc`, `0-III`).
    - **JNET Classification:** Vascular and surface pattern evaluation (`Type 1`, `Type 2A`, `Type 2B`, `Type 3`).
    - Metric size estimation with dynamic distractor generation.
- **Immersive Interaction (Virtual Reality & Hardware):**
  - **Virtual Reality:** Built specifically for VR headsets via the OpenXR standard, featuring full 6DoF interaction with XR Interaction Toolkit and XR Hands support (Meta Quest, HTC Vive).
  - **Physical Endoscope Hardware:** Serial RS-232/USB communication support ([SerialUSB.cs](Assets/Scripts/SerialUSB.cs)) to connect custom training replicas or physical mechanical handles.
- **AI Virtual Supervisor:**
  - Spoken voice feedback, live guidance, and 3D chat bubbles in the virtual environment.
  - Dual support for **Local Offline LLMs** ([Ollama](https://ollama.ai/)) and **Cloud Providers** (OpenAI GPT-4o, Whisper STT, TTS) with function calling capabilities.

---

## System Architecture

The simulation environment is organized around a central virtual hub (`MainRoom`) connecting three specialized secondary rooms:

| Module | Scene | Purpose | Target User |
| :--- | :--- | :--- | :--- |
| **Main Room** | `MainRoom.unity` | Central hospital hub, session setup, and patient model browser | All users |
| **Model Editor** | `ModelEditor.unity` | Authoring of patient-specific 3D colon models with placed pathologies | Educator / Clinical Expert |
| **Coverage Training** | `CoverageTraining.unity` | Systematic mucosal surface inspection with speed and angulation monitoring | Trainee |
| **Polyp Training** | `PolypTraining.unity` | Detection and multi-criteria diagnostic classification of polyps | Trainee |

---

## Quick Start

### Prerequisites
- **Unity Hub** con **Unity 6000.4.0f1 (Unity 6.4)**.
- **Visual Studio 2022** or **Rider** with Unity workload.
- Supported VR Headset compatible with OpenXR (Meta Quest, HTC Vive, Valve Index, etc.).
- *(Optional)* [Ollama](https://ollama.ai/) installed locally if you wish to run local AI assistance with any model of choice (`llama3.2`, `mistral`, `biomistral`, etc.).

### Running the Simulator
1. Clone this repository to your local machine.
2. Open the project folder in **Unity Hub** using version `6000.4.0f1`.
3. Open the entry scene: `Assets/Scenes/StartupScene.unity`.
4. Press **Play**. The application will initialize global singletons and transition to the `MainRoom` hub.
5. In `MainRoom`, interact with the model browser, select a case, and walk through the practicable doors to enter any training or editing room.

> [!IMPORTANT]
> A VR headset compatible with OpenXR is required to run and experience the training and editing environments. For testing in the Unity Editor without wearing a headset, you can use the integrated Unity XR Device Simulator / Mock HMD.

---

## Documentation Index

For detailed engineering and implementation notes, consult the relevant sections in [**OVERVIEW.md**](OVERVIEW.md):

- [Overview & Technical Specifications](OVERVIEW.md#1-ficha-técnica)
- [Scene Lifecycle & Flow Controller](OVERVIEW.md#2-mapa-de-escenas-y-ciclo-de-vida-de-la-aplicación)
- [Script Subsystems Breakdown (Folder by Folder)](OVERVIEW.md#3-subsistemas-y-arquitectura-de-código-assetsscripts)
  - [Geometry & Math Library (`Common/`)](OVERVIEW.md#31-common-geometría-computacional-algoritmos-y-utilidades-de-malla)
  - [Spatial 3D UI Framework (`CustomUI/`)](OVERVIEW.md#32-customui-sistema-de-interfaz-3d-y-ergonomía-espacial)
  - [Anatomy(`LargeIntestine/`)](OVERVIEW.md#37-largeintestine-definición-anatómica-del-colon)
  - [AI & Audio Assistants (`SceneCommons/`)](OVERVIEW.md#396-scenesscenecommons-módulos-compartidos)
- [Software Design Patterns & Architecture](OVERVIEW.md#4-patrones-de-diseño-y-calidad-del-software)
- [Setup & Custom AI Integration Guide](OVERVIEW.md#5-guía-de-inicio-para-desarrolladores)
- [HDRP Rendering Pipeline & Shaders](OVERVIEW.md#6-pipeline-gráfico-hdrp-y-shaders-personalizados)
- [Clinical Scoring & Methodology](OVERVIEW.md#7-flujo-metodológico-clínico-y-métricas-de-evaluación-scoring)
- [Real-Time VR 90 FPS Performance Optimization](OVERVIEW.md#8-rendimiento-en-tiempo-real-y-estrategia-para-90-fps-en-vr)
- [Hardware Serial USB Protocol](OVERVIEW.md#9-protocolo-de-comunicación-con-hardware-físico-serialusb)
- [File Formats & Model Data Models](OVERVIEW.md#10-formato-de-archivos-ligc-y-estructura-de-datos-de-pacientes)
- [Troubleshooting Guide](OVERVIEW.md#11-guía-de-resolución-de-problemas-troubleshooting)

---

## Research Context & Citation

Colonoscopy training has traditionally relied on supervised practice with real patients, complemented by mechanical and computerized simulators. Existing simulators often face two limitations: limited scenario libraries that are costly to expand, and a focus on manual navigation skill with little support for the diagnostic side of the procedure.

LoGiViT addresses both limitations through procedural, parameterized anatomical generation and dedicated modules for navigation and diagnosis, supported by AI-assisted feedback.

If you use LoGiViT in your research, please cite:

```bibtex
@phdthesis{lugilde2026logivit,
  author       = {Adrián Lugilde López},
  title        = {Contributions to the modeling and simulation of colonoscopy using virtual reality},
  school       = {Universidade de Vigo},
  year         = {2026}
}
```

---

## Acknowledgments

Developed by **Adrián Lugilde López** at the **Universidade de Vigo**, under the supervision of **Fernando Ariel Mikic Fonte** and **Manuel Caeiro Rodríguez**, as part of the doctoral dissertation *Contributions to the modeling and simulation of colonoscopy using virtual reality*.

## Contact & Links

- 🌐 **Project Website:** [https://logivit.atlanttic.uvigo.es/](https://logivit.atlanttic.uvigo.es/)
- ✉️ **Contact:** [adrian.lugilde@det.uvigo.es](mailto:adrian.lugilde@det.uvigo.es)
