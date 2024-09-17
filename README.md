# Chain
Unity package for simplified representation of the flexible cable chain.

![Chain_1.gif](Documentation%2FChain_1.gif)

# Install Package
Insert package using the **Unity Package Manager** or directly in `Packages/manifest.json`.

```json
    {
        "dependencies": {
            "com.preliy.chain": "https://github.com/Preliy/Chain.git#upm",
            "com.unity.render-pipelines.universal": "14.0.11"
        }
    }
```
> [!NOTE]  
> Universal Render Pipeline is used.

# How to use
Add `Chain.cs` component to GameObject.

![ChainInspector.png](Documentation%2FChainInspector.png)

+ Add Prefabs to the `Prefabs` List and define the `Item Offset`. The Prefabs are instantiated one after the other with the offset along the spline.
+ Define the `Length` and `Radius` for the spline. 
+ Use `Gizmos` parameter to display the gizmos and see current spline 
+ `Use Main Root` reparent while starting the chain items to the main root of the scene. You can use it to gain performance for large chains.

> [!NOTE]  
> The parameterization and item instantiation happens along local Forward direction (Vector3.Forward).

The position change for chain is possible with direct manipulation with `Position` property or by assigning the `Follow Target`. 



