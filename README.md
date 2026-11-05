# Magic Murder 3d
The game is called like that, but it's not 18+, trust me.
### Engine
It works on a crappy CPU rendered graphics, but it supports multi-core rendering, so buying a Threadripper will help
### Gameplay
Not very interesting, but you can place and revoke blocks by using right and left clicks on your mouse. A lot of features can be added tho.
### Limitations
World is 9 quintillion blocks in every direction from the spawn at 0, 0, 0, this may be useful for bigger contraptions. The resolution is 960x960, but trust me you won't want to make it bigger
### Performance
**~10-20 fps** on multi-core rendering on my Ryzen 9 5950x, CPU usage ~70-80%
### Saves
It saves your game each 20 seconds and before closing, use P key to save the game to the persistent storage device.
### Save format
Each chunk is in it's separate file `chunk.{cx}.{cy}.{cz}.bin`, and stored like that:
```
[8 bytes chunk X]
[8 bytes chunk Y]
[8 bytes chunk Z]
[524288 bytes block data]
```
Each block is stored as is and contains 2 bytes of data.
### Screenshots of the real gameplay
<img width="1201" height="1239" alt="screenshot1" src="https://github.com/user-attachments/assets/f690ff4a-d980-4d37-bccf-6d370b2c40f4" />
<img width="1201" height="1239" alt="screenshot2" src="https://github.com/user-attachments/assets/1aedc21d-4669-44b4-b65b-981f70b11885" />
<img width="1201" height="1239" alt="screenshot3" src="https://github.com/user-attachments/assets/1a8368b8-8a71-4e03-b33f-d54aa1b72274" />
<img width="1201" height="1239" alt="screenshot4" src="https://github.com/user-attachments/assets/2968f8b2-0859-44be-9eb2-d5d169f60d95" />
