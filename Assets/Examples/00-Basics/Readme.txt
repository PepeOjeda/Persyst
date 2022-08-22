Welcome one, welcome all!

To the simplest possible application of Persyst.

Have a look at the scene. You will find that the "Managers" GameObject has a "UIDManager" and a "GameSaver" components. They are both just drag-and-drop ready, you don't need to configure anything.

Each of the other objects has a "PersistentObject" component. This marks them as relevant for Persyst. Again, all you need to do is add the component, they configure themselves.

The last thing in there is a "BasicsTest" script. It's just a MonoBehaviour that implements ISaveable (no methods to implement, it's just a tag), and contains fields marked as [SaveThis].



To test the basic functionality:

Enter Play Mode, assign values to the fields of of each BasicsTest through the inspector, and then hit the "Write" button on the GameSaver.
That will create a json file in the Assets folder (you can customize the path when calling the write function through code).

Now, exit Play Mode. The values will reset to nothing (oh no!).

Enter Play Mode again, and hit "Read" on the GameSaver. You'll see the saved values be recovered and assigned to the correct fields on the correct object.
Yay!