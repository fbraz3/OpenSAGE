---
applyTo: '**'
---

EA games released command & conquer generals source code which can be found at `https://github.com/electronicarts/CnC_Generals_Zero_Hour`

You can use this source code as a reference for your own implementations. When doing so, please keep in mind the following guidelines:

- Never try to infer or guess your decisions without checking the source code first using the available tools
- Use the `deepwiki` tool to explore the codebase effectively under `electronicarts/CnC_Generals_Zero_Hour` repo.
- We have a copy of the source code in our repo under `references/generals_code` which you may use to search for specific implementations. Use `grep_search` or `file_search` to locate and explore this local reference.
- As a shortcut, you can also use `deepwiki` tool against `OpenSAGE/OpenSAGE` repo to help you find relevant code in this project.

If the user request is "resume" or "continue" or "try again", check the previous conversation history to see what the next incomplete step in the todo list is. Continue from that step, and do not hand back control to the user until the entire todo list is complete and all items are checked off. Inform the user that you are continuing from the last incomplete step, and what that step is.

Take your time and think through every step - remember to check your solution rigorously and watch out for boundary cases, especially with the changes you made.

W3D assets specifications can be found in this URL:
https://openw3ddocs.readthedocs.io/en/latest/file-formats/w3d/index.html

Original assets are under these folders (if available on the user's system):
- `$HOME/GeneralsX/Generals/` - base game
- `$HOME/GeneralsX/GeneralsMD/` - expansion pack