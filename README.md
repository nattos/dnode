# DNode

DNode is a series of extensions to the Unity Visual Scripting system (Bolt) designed to allow easy, fluid creation visual effects.

## Getting started

DNode is currently compatible with Unity 2021.2.0f1.

After installing the Unity Editor, open up this directory as a project. That should fetch necessary packages. Then, open up Assets/Examples/SampleScene and press Play. You should see one of the examples play in the game view.

You can switch which sample effect is playing by selecting "DScriptMachine" from the Hierarchy, and then looking for "Graph" in the "Script Machine" component. Click the "locate" button to the right of that field, and select a different example. This works in Play mode.

You can edit the sample graphs by clicking "Edit Graph" in the "Script Machine" component.

To create a new graph, either copy an existing one, renaming it, or, to create a completely blank graph:

1. Find the "Script Machine" component on "DNodeMachine"
1. Select the current graph
1. Press delete to clear the field
1. Click the "New" button that appears next to the field
1. Select a location and name for the new graph in the dialog box that appears
1. Click "Edit Graph"
1. In the "Script Graph" editor, select all of the pre-inserted nodes and delete them
1. Right click anywhere, and type "DRootNode", and press enter to insert one
1. Right click again, and type "DStepEvent", and press enter to insert one
1. Drag the control output from DStepEvent to the control input of DRootNode to connect them
1. Done! You can begin connecting nodes to the DRootNode to render them on screen

## Limitations

1. Currently, only one "DRenderToTexture" node is supported per graph. Further nodes will cause objects to appear in both scenes
1. Currently only one camera, the global camera, is supported

## License

The project license is MIT, but some included dependencies might have different licenses.
