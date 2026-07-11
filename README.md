# Unity Enemy AI State Machine

A configurable enemy AI system built in Unity using a finite state machine, Unity NavMesh, and weighted decision-making.

The AI was designed for a melee combat prototype during a 48hrs game jam where enemies react dynamically to player actions instead of following scripted behavior.

---

## Overview

Rather than executing predefined sequences, enemies continuously evaluate the player's actions and choose their next behavior based on configurable probabilities.

Each enemy can have different personalities by adjusting inspector values such as aggression, reactiveness, follow delay, and decision timing.

This allows multiple enemies using the same script to behave differently without requiring separate implementations.

---

## Features

- Finite State Machine architecture
- Weighted random decision making
- Configurable enemy personalities
- Dynamic reaction to player attacks
- Randomized follow and decision delays
- NavMesh pathfinding
- Smooth movement and rotation
- Inspector-driven balancing
- Easy to extend with additional states

---

## AI States

- Idle
- Follow
- Wander
- Approach
- Attack
- Stay
- Flee

---

## Behavior Examples
<p align="center">
  <img src="https://github.com/user-attachments/assets/564d0c6c-64f6-4e1e-aae8-4aeb34f22877" width="32%">
  <img src="https://github.com/user-attachments/assets/d7e3a7a0-2419-4f3b-9dfb-da62a579cc8b" width="32%">
  <img src="https://github.com/user-attachments/assets/02972007-0363-4959-9f1f-dfdcb6dbe08a" width="32%">
</p>


### Normal Gameplay

Enemy periodically decides whether to:

- Follow the player
- Stay in place
- Aggressively approach

using configurable weighted probabilities.

---

### Player Attacks

When the player attacks, the enemy immediately evaluates the situation and may:

- Flee
- Attack back
- Stay defensive
- Reposition

The probabilities are affected by the enemy's aggression multiplier.

---

### Player Dashes

While the player is dashing the enemy temporarily switches to wandering behavior before resuming normal decision making.

---

## Configurable Parameters

The system exposes multiple balancing values directly in the Unity Inspector, including:

- Aggression
- Reactiveness
- Follow delay
- Decision interval
- Attack distance
- Flee distance
- Wander radius
- State weights

No code changes are required for balancing enemy behavior.

---

## Technologies

- Unity
- C#
- Unity NavMesh
- Animator

---

## Future Improvements

Potential extensions include:

- ScriptableObject AI profiles
- Squad coordination

---

## Purpose

This repository serves as a technical showcase of gameplay programming techniques including AI architecture, configurable systems, state machines, and gameplay decision making.
