# Copilot Instructions

## General Guidelines
- Provide concise, code-focused assistance for WPF projects.
- Use ViewModel property (IsIntensityAtLeast2) for UI threshold logic instead of a separate converter file.
- Use the following keybinds for mock operations: StartMock = `Ctrl+Shift+T`, StopMock (Disconnect) = `Ctrl+Shift+Q`.

## UI Rules
- When `CurrentIntensity >= 2`, both the intensity numeric text and the 'EARTHQUAKE DETECTING' message must be white.
- When `CurrentIntensity < 2`, both the intensity numeric text and the 'EARTHQUAKE DETECTING' message must be black.

## Project-Specific Rules
- Active project located at `C:\Users\halka\Documents\GitHub\yureteruWPF` on branch `main`.