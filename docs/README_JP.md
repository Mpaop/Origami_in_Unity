# Origami_in_Unity

***
　Unity上でメッシュを折り紙のように折れるデモプロジェクトです。 <br />
<br />
[Go to English Readme](https://github.com/Mpaop/Origami_in_Unity/blob/master/README.md)<br />
<br />
<br />
<img src="https://raw.githubusercontent.com/wiki/Mpaop/Origami_in_Unity/images/fold01.gif" alt="Demo" width="700"/>
<br />
<br />
***

## 環境
　Unity (2019.2.19f1)  
<br />
     

## デモの使い方

     1. プロジェクトをクローン後、Scenesフォルダの "FoldDemoScene" をプレイしてください。    
        シーンが起動すると、画面中央に四角いメッシュが表示されるはずです。  
     
     2. 画面上でドラッグ&クリックすることで、直線を引くことが出来ます。  
        メッシュを横切るように線を引いてみてください。  
     
     3. 画面右下の"Fold"ボタンをクリックしてください。メッシュを折る処理が実行されます。
     
     (不具合が発生したり、新しいメッシュで折りたくなった場合、画面右下の"Reset"ボタンを押してください。シーンの再ロードが行われます。)  
  
***  
<br />

## ゲーム上の使用例
<img src="https://raw.githubusercontent.com/wiki/Mpaop/Origami_in_Unity/images/demo02.gif" alt="Gameplay" width="600"/>  

***

## 詳細
　このプロジェクトのソースをゲームなどで使いたい場合、まずはScriptsフォルダの直下にある"MeshCreaseDrawer.cs"の内容から確認することをお勧めします。  
　このクラスは画面上に線を引く機能を持つ他、実際にメッシュを折るクラスを呼び出す役割を担っています。  


### **Origami_Demo (namespace)**
　この名前空間にはMeshCreaseDrawerというクラスが含まれています。あくまでデモ用に作成されたコードであるため、  
紙を折る処理を実行する上では必要ありません。  
   - **クラスなどの概要:**  
     - MeshCreaseDrawer
       - 画面上に直線を引く。 
       - MeshFoldMachineなどメッシュを折る処理を実行する他クラスを呼び出す。  

### **Origami_Fold (namespace)**
　この名前空間には、MeshFoldMachineというメッシュを折る処理の中心核を担うクラスが含まれています。  
   - **クラスなどの概要:**  
     - MeshFoldMachine  
       - メッシュを折る。  
       - 本デモにおいて、このクラスはMeshCreaseDrawerより直線の始点と終点の座標を"InitializeFold"というメソッドを介して受け取ります。これら二点からなる直線に沿ってメッシュは分割されます。  
         この処理を行ってから、"FoldMeshToAngle"というメソッドで折りたい角度(ラジアン)の値を指定すと、メッシュを折ることが出来ます。最後に、"EndFold"というメソッドを呼ぶと、MeshFoldMachineの内部的な後処理等が行われます(※"EndFold"の仕様は今後変更される予定にあります)  
  
### **Origami_Mesh (namespace)**  
　この名前空間は、メッシュや頂点など、MeshFoldMachineが処理を行う対象となるクラス等がまとめられています。
  - **クラスなどの概要:**  
    - OrigamiBase  
      - MeshFoldMachineにて扱うメッシュの抽象クラス。UnityのMeshオブジェクトを管理する他、  
        回転したベクトルの値を返す機能などを持っています。  
    - OrigamiMesh  
      - OrigamiBaseを継承するクラス。独自の機能はあまり多くありませんが、折り目のメッシュに接した頂点の値を  
        調整するなどといった処理を行っています。  
    - CreaseMesh  
      - メッシュを折る際に生じる隙間を埋めるメッシュクラス。OrigamiMeshのオブジェクトを折る際、  
        Z-ファイティングを回避する目的で頂点を少しだけずらすといった調整が必要となります。  
        その際、メッシュを紙より分離させないためにCreaseMeshのメッシュを生成します。  
      - CreaseMeshクラスの仕様は近い将来変更予定にあります。  
    - Crease
      - CreaseMeshは基本的に二個一組で扱うため、その管理を行うクラス。
        メッシュを折る箇所を埋める目的で生成するため、Readmeやソースのコメントでは便宜的に折り目と呼んでいきます。
    - MeshVertex
      - MeshVertexは読み取り専用の構造体であり、メッシュを折る際に必要となる情報を一つに束ねるという意図で作られています。  
    - MeshVertices
      - MeshverticesはMeshVertexオブジェクトを生成するために必要なデータをリスト毎に有するクラスです。  
        MeshVertexのリストを宣言した方が管理しやすいように思われるかもしれませんが、UnityのMeshクラスを扱う関係から分けています。  
    - IFoldMeshCallbacks
      - コールバックメソッドを定義するためのインターフェイス  

### **Origami_Result (namespace)**  
　この名前空間は、OrigamiMeshオブジェクトなどが折られる際に必要とする情報などを格納する構造体をまとめたものです。  
  - **クラスなどの概要:**  
    - FoldResult  
      - 頂点を折る(回転させる)上で必要なデータを持つ読み取り専用の構造体。  
    - OrigamiFoldResult
      - FoldResultをラップし、更にOrigamiMeshの頂点を折る上で必要なデータもまとめた構造体。  
    - OrigamiFoldResults  
      - 複数のOrigamiFoldResultをメンバーとして持つ構造体。各メンバーはOrigamiMeshオブジェクトの頂点に対応します。  
    - CreaseGenerationInfo/CreaseGenerateResults/CreaseFoldResult/CreaseFoldResults  
      - Crease/CreaseMeshesの仕様が変更されるため、上記の構造体は破棄、または再設計の対象となる予定です。  
    - IFoldResults
      - 上の構造体に実装させたいメソッドをまとめたインターフェイス


### **Origami_Utility(namespace)**  
　この名前空間には、OrigamiUtilityというクラスが含まれています。
  - **クラスなどの概要:**  
    - OrigamiUtility  
      - 便利なメソッドや定数などを提供するクラス。

***

## **今後の予定**
  - 折り目のアルゴリズムを変更。  
  - 折る際の計算量を減らすための修正。  
  
***

## **確認されている不具合**  
  - 折る際、折り目のメッシュが折り紙の中からはみ出てくることがあります。現在修正中です。  
  - 時々、折る処理に失敗した後に再度折り紙を折ろうとすると、メッシュの形状が極端に崩れる場合があります。  
  
