# Origami_in_Unity

***
This is a demo project where we can fold meshes like Origami. <br />
<br />
[日本語版Readmeはこちらから](https://github.com/Mpaop/Origami_in_Unity/blob/master/docs/README_JP.md)<br />
<br />
<br />
<img src="https://raw.githubusercontent.com/wiki/Mpaop/Origami_in_Unity/images/fold01.gif" alt="Demo" width="700"/>
<br />
<br />
***

## Requirements
Unity (2019.2.19f1)  
<br />
     

## How to play the demo

     1. After cloning the project, play the "FoldDemoScene" in the Scenes folder.  
     You should see a square mesh in the middle of the screen.  
     
     2. You can draw straight lines with your mouse by clicking and dragging on the screen.  
     Draw a line that goes across the mesh.  
     
     3. Press the "Fold" button at the the bottom right corner of the screen. The mesh will start to fold :)  
     
     (If something goes wrong or you want to retry folding with a new mesh,  
      press the "Reset" button at the bottom right of the screen. The scene will be reloaded)
  
***  
<br />

## Gameplay using Origami_in_Unity
<img src="https://raw.githubusercontent.com/wiki/Mpaop/Origami_in_Unity/images/demo02.gif" alt="Gameplay" width="600"/>

***

## Descriptions of this project
If you are interested in using this project, it is advised to first check out "MeshCreaseDrawer.cs" under the Scripts folder.
The purpose of this class is to draw lines on the screen and call methods of other classes that do the actual folding.  


### **Origami_Demo (namespace)**
This namespace consists of only one class, that is MeshCreaseDrawer. It is made for demonstration purposes, and is not required for folding paper operations.  
   - **Contents of this namespace:**  
     - MeshCreaseDrawer
       - Draws lines on the screen. 
       - Calls methods of other classes (such as MeshFoldMachine) that does the actual folding.  

### **Origami_Fold (namespace)**
This namespace has MeshFoldMachine, which is the core module for folding the meshes. This class can be used by creating an instance.
   - **Contents of this namespace::**  
     - MeshFoldMachine  
       - Folds the mesh.  
       - In the demo, this class receives two vertices that were used to present the line on the screen via "InitializeFold". Using them, it divides the meshes along the line. After that, the MeshCreaseDrawer sets the amount of radians it wants the mesh to fold by calling "FoldMeshToAngle." Finally, "EndFold" is called to cleanup whatever it needs to. (Contents of EndFold are due to change in the future, so I won't go into much detail, sorry)
  
### **Origami_Mesh (namespace)**  
This namespace is a collection of meshes and vertices used by the MeshFoldMachine to fold paper.
  - **Contents of this namespace::**  
    - OrigamiBase  
      - An abstract class of all the meshes. It's main functionalities are to update the Unity Mesh object, as well as return rotated vectors.
    - OrigamiMesh  
      - Mesh class that derives from OrigamiBase. Doesn’t have too many functions of its own but, vertices of this mesh need a little more adjustments during the folding stage, if they are connected to a Crease mesh.
    - CreaseMesh  
      - Mesh class that is used to fill in spaces when folded. Spaces need to be created between Origami meshes in order to avoid Z-fighting. Crease meshes are created in order to avoid meshes "detaching" from the paper. Specifications of the CreaseMesh class are bound to change in the near future.
    - MeshVertex
      - MeshVertex is a readonly struct that wraps information we want bundled together for folding meshes.
    - MeshVertices
      - Meshvertices is a class that has lists of types that we can combine to create a MeshVertex object. While it may seem convenient to have a single list of MeshVertex objects, it is better to have them separated for working with Unity's Mesh class.
    - IFoldMeshCallbacks
      - An interface that defines methods to be used as callbacks.


### **Origami_Result (namespace)**  
This namespace is a collection of structs that are referred to by classes such as the OrigamiMesh class during the folding stage as it contains arithmetic results meant to be used for this purpose.
  - **Contents of this namespace::**  
    - FoldResult  
      - An readonly struct that contains data for a vertex to fold.  
    - OrigamiFoldResult
      - A struct that wraps a FoldResult and adds other members to be used for folding OrigamiMesh objects.  
    - OrigamiFoldResults  
      - A struct that containts multiple OrigamiFoldResult members. Each member corresponds to a vertex of the mesh it refers to.
    - CreaseGenerationInfo/CreaseGenerateResults/CreaseFoldResult/CreaseFoldResults  
      - The folding logic for Crease Meshes are due to change in the near future. Therefore all these structs are bound to be ridden or redesigned.
    - IFoldResults
      - An interface to be implemented into FoldResult structs


### **Origami_Utility(namespace)**  
This namespace currently contains OrigamiUtility.
  - **Contents of this namespace::**  
    - OrigamiUtility  
      - A class that provides useful functions.

***

## **Moving Forward**
  - Fix crease logic.  
  - Reduce the amount of calculations done per fold.
  
***

## **Bugs**  
  - Sometimes, creases will stick out of the origami. This is currently being worked on.
  - Sometimes, folding the meshes after being told by the MeshFoldMachine that it is unable to fold causes the meshes to break.
  
