# JomonKaenGazeData Unity Application

Unity application to collect eye gaze, voice & QNA data on Pottery and Dogu.

## Download & Installation

```
git clone --depth 1 https://github.com/luhouyang/JomonKaenGazeData.git
```

## Usage

[**LATEST DOCUMENTATION v3**](https://docs.google.com/document/d/1hs8zHF2yzrMDRXTj319fBOcU91ZXBHdh00ar9nUG-KI/edit?usp=sharing)

## CHANGELOG

**v3 6 JUNE 2025**
---
Experiment setting 3

- Removed pop up and replaced with continuos keypad input to collect live emotion / reaction data

- Re-added voice recording after eye gaze recording

- Created scenes to seperate groups (due to RAM limitation of HoloLens 2, only 4 groups can be deployed at once, but to achive optimal performance deploying 3 groups is ideal)

**v2 ~29 MAY 2025**
---
Experiment setting 2

- Removed voice recording and replaced with pop up QNA questions when there is fixation on any part of pottery or dogu detected

- Implemented experiment switching

- Seperation of concerns for experiment, model group, model, experiment flow control and recording flow control

**v1 ~17 MAY 2025**
---
Experiment setting 1

- Record eye gaze data on pottery and dogu

- Use voice commands to control the flow of experiment

- Outdated [documentation v1](https://docs.google.com/document/d/1hKC6ZbaYykXuG3uE8nnfWEo0_yILdFh1-1NuDhXU6lI/edit?usp=sharing)
