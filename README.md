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
   - **Classes included in this namespace:**  
     - MeshCreaseDrawer
       - Draws lines on the screen. 
       - Calls methods of other classes (such as MeshFoldMachine) that does the actual folding.  

### **Origami_Fold (namespace)**
This namespace has MeshFoldMachine, which is the core class for folding the meshes. This class can be used by creating an instance.
   - **Classes included in this namespace:**  
     - MeshFoldMachine  
       - Folds the mesh.  
       - In the demo, this class receives two vertices that were used to present the line on the screen. Using them, it divides the meshes along the line. 
  
### **Origami_Mesh (namespace)**  
  - **Classes included in this namespace:**  
    - OrigamiBase  
      - Abstract class.
    - OrigamiMesh  
      - Mesh class that derives from OrigamiBase.
    - CreaseMesh  
      - Mesh class that is used to fill in spaces when folded


### **Origami_Result (namespace)**  
  - **Classes included in this namespace:**  
    - FoldResults  
      - An readonly struct that carries data to be used while folding.
    - OrigamiFoldResults  
      - A struct for folding OrigamiMesh objects.
    - CreaseFoldResults  
      - A struct for folding CreaseMesh objects.


### ** Origami_Utility(namespace)**  
  - **Classes included in this namespace:**  
    - OrigamiUtility  
      - A class that provides useful functions.

***

## **Plans**
  - Fix crease logic.  
  - Reduce the amount of calculations done per fold.
  
### **Bugs**  
  - Sometimes, creases will stick out of the origami. This is currently being worked on.
  - Sometimes, folding the meshes after being told by the MeshFoldMachine that it is unable to fold causes the meshes to break.
  
