This one's short.

Have a look at the ReferenceLoop_MB class. You will see that it has two fields, which are references to objects. Those two objects are saveable, and they reference each other.
Since they will be serialized by value, the naive way to proceed (creating a new instance every time a reference is found) would keep going forever and eventually crash the program.

So, to solve this, Persyst will keep track of reference loops. When one is detected, the reference that is looping will just be ignored. This is not great if that reference was important to the functioning of your code, of course. So I would recommend not serializing reference loops at all.

Run the scene, hit "write" on the gameSaver and have a look at the Json file to better understand how this works.