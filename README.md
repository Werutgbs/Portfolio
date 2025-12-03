# Ilia Guchashvili - Unity Developer Portfolio

Senior Unity Developer specializing in multiplayer systems, performance optimization, and scalable game architecture.

📧 werutgbs@gmail.com | 💼 [LinkedIn](https://www.linkedin.com/in/ilia-guchashvili-310b1321a/) 

---

## 🎮 Featured Projects

### 1. Alpaca Dash - Multiplayer Racing Game
> Multiplayer alpaca racing game with physics-based movement and dynamic camera system

![Gameplay GIF](https://media1.giphy.com/media/v1.Y2lkPTc5MGI3NjExYWd6bDk1c3drODk5aHoxdGRjc2N1bThvdDF6bnYxdHRqM3VvZG5sOCZlcD12MV9pbnRlcm5hbF9naWZfYnlfaWQmY3Q9Zw/7FVOtuWaQ6fBEOX1Dz/giphy.gif)

**What it does**: Multiplayer racing game featuring dynamic camera work, physics-based character movement, and smooth online synchronization.

**Technical Highlights**:
- **Dynamic Camera System**: FOV-based zoom with interpolated movement and look-ahead prediction for cinematic racing experience
- **Physics-Based Movement**: Event-driven state management with interface-based architecture for responsive controls
- **Procedural Track System**: Bezier spline system with real-time curvature calculation affecting gameplay mechanics

**Tech Stack**: Unity, C#, Physics System, Custom Networking, Bezier Math

**Code Samples**:
- [BirdEyeCamera.cs](./Racing%20Game/BirdEyeCamera.cs) - Dynamic camera implementation
- [RacerController.cs](./Racing%20Game/RacerController.cs) - Character controller with state management
- [RaceTrack.cs](./Racing%20Game/RaceTrack.cs) - Procedural track generation

[▶️ Watch Full Gameplay Video](https://www.youtube.com/watch?v=eKRgoRmziBs) | [📥 Play WebGL Build](https://play.almightyalpacas.com/)

---

### 2. Basic RTS - Data-Oriented Design Demo
> Large-scale RTS demonstrating Unity DOTS/ECS for managing thousands of units

![RTS Demo GIF](https://media2.giphy.com/media/v1.Y2lkPTc5MGI3NjExMmE4Y212NXNmOHhwYnhucWh3OG5uZ2J4MzJ4aTVjZnFmNTV6eWd6YSZlcD12MV9pbnRlcm5hbF9naWZfYnlfaWQmY3Q9Zw/RcKq2lR7udgJXzuWdg/giphy.gif)

**What it does**: Real-time strategy game demonstrating high-performance unit management using Unity's Data-Oriented Technology Stack (DOTS). Handles 1000+ units simultaneously with smooth performance.

**Technical Highlights**:
- **DOTS/ECS Architecture**: Full data-oriented design for maximum performance
- **Burst Compiler Integration**: Parallel job scheduling for movement and combat systems
- **AI Combat System**: Entity-based targeting, pathfinding, and projectile spawning
- **Memory Efficient**: Reduced memory footprint by 70% vs traditional GameObject approach

**Key Metrics**:
- 1000+ units at 60fps on mid-tier hardware
- 10x performance improvement over traditional OOP approach
- <2ms per frame for unit AI calculations

**Tech Stack**: Unity DOTS, ECS, Burst Compiler, Job System, C#

**Code Samples**:
- [UnitMoverSystem.cs](./RTS%20Game/UnitMoverSystem.cs) - Burst-compiled movement system
- [ShootAttackSystem.cs](./RTS%20Game/ShootAttackSystem.cs) - AI combat implementation
- [UnitMoverAuthoring.cs](./RTS%20Game/UnitMoverAuthoring.cs) - GameObject to Entity conversion


## 📫 Contact & Links

- **Email**: werutgbs@gmail.com
- **LinkedIn**: [Your Profile](https://www.linkedin.com/in/ilia-guchashvili-310b1321a/)
