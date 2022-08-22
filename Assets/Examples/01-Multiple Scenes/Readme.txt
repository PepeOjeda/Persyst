In this case, we have multiple scenes that can be loaded at the same time.

Unity's serialization system will complain about having cross-scene references, but Persyst does support them (with some small caveats).

Let's do two tests:

First, open all three scenes at the same time. You will see the "Persyst" scene contains the managers. This is the recommended workflow when having additive scenes: keeping the managers in their own separate one that is never unloaded.

Enter play mode and find the CrossSceneRefTest script attatched to the object in scene 1. If you try to assign to its field a reference to the object in scene 2, you will find the unity inspectot wont let you. This is because, as previously mentioned, unity does not serialize cross-scene references. Still, getting the reference is of course possible, you must just assign it through code.

Press the button "get reference" on the CrossSceneRefTest script to assign it. Now, write the save file, as in the previous example.

Leave play mode, and enter it again. When you press "Read" on the GameSaver, the reference will be restored correctly.

-----

Now for the second test, a bit more complicated.
Before anything , you need to add Scene2 to the build settings, so the sceneManager will let you load it at runtime.

Keeping the same saveFile from before, start playmode with only the Persyst scene and Scene1. When you hit "Read", the reference will continue to be null, as the referenced object is not loaded. However, if you now press "Load scene" to get Scene2 loaded, you will see the reference immediately det updated to the correct value. This is because of the PendingReferences system, which you can read more about in the docs.

Pretty cool, huh?


Now, the limitations. If you have both scenes active, assign the reference, then remove scene2, then write the file, the reference will be serialized as null, because that is the value at the time of writing.
Also, if you do all the steps in the second test, then remove scene2, then load it again, the reference will no longer be restored automatically, as it has already been remove from the PendingReferences system.