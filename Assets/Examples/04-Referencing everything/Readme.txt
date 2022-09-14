This is a complicated one. Rather than explain everything here, I encourage you to have a look at the code and read the comments. Try to change things, and see what happens.

In essence, we are recreating the structure of Persyst itself (although a bit simpler) for one particular type. We have a singleton that manages the relationship between UIDs and object instances (the item_tracker), and a process for automatically retrieving the instances when setting the UIDs (the setter of the ItemWrapper uid). That way, we only need to serialize the UID when referencing an object.

To see the basic behaviour, use the buttons on the Item_MB class.
First, start the game. Then create either two separate objects or one shared by two references with the buttons.
Then, save the game, and exit play mode.
Enter play mode again, read the file. 
The objects will not be loaded automatically, because we need to handle the order by hand (see exlanation below). Go to the Item_MB class and hit "recover references".

If you created two objects, you will see that two different instances now exist (this was really inconvenient to show in the inspector without complicating the code, so currently all you can see is that they both have different values for an integer field, displayed as "printA" and "printB"). 

If you created a single object, you will see that both fields show the same value. If you change the value of the field in itemA, both will change, because there is only a single instance of the object. 

So, we correctly serialized references to non-Unity objects. Hurray!



However, there are several catches if you want to make this work for your game: 

- Loading order. It is imperative that the manager class (item_tracker, in this case) is loaded before *any* item references are to be retrieved. This is because the list of existing instances is kept there. If this has not been loaded, there is nothing to reference.
See the docs if you need a refresher on how to control the loading order.

- Wrapper class. Not a big thing, but you need to use a wrapper for the references, to store the uids (ItemWrapper in this example). It's slightly incovenient, but that's all.

- Unity serialization. This one is a real problem, because of constructors and initialization functions and all that. 
Long story short is: don't do it. 
If you are planning to serialize references like this, don't use [SerializeField] on them. (unless you know what you are doing and know how to untangle this mess)
