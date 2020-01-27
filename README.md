# 折り紙(メッシュ)をUnityで折るプロジェクト

##### 使用エンジン： Unity (2019.2.19f1)

<img src="https://raw.githubusercontent.com/wiki/Mpaop/Origami_in_Unity/images/fold01.gif" alt="Demo" width="700"/>

#### 動作はFoldDemoSceneにて確認出来ます。（以下、FoldDemoSceneの操作方法)
     1. シーンをプレイ状態にすると、メッシュが表示されます。
     2. 画面上でマウスをクリックした状態でドラッグし、折り目となる線をメッシュと重なるように引いてください。
     3. 引き終えたら、右下にある「折る」というボタンをクリックしてください。メッシュを折る処理が行われます。
     ※ 何らかの不具合や、メッシュを初期状態に戻したい場合は、右下の「Reset」というボタンをクリックしてください。シーンの再ロードが行われます。
  
***
## ゲームで用いた例
<img src="https://raw.githubusercontent.com/wiki/Mpaop/Origami_in_Unity/images/demo02.gif" alt="ゲームの使用例" width="600"/>

***

## namespaceと各クラスの概要：

### **1. Origami_Fold**
  - MeshFoldMachine  
  メッシュを折るクラス
  
### **2. Origami_Mesh**
  - OrigamiBase
  折り紙に用いるメッシュの抽象クラス
  - OrigamiMesh  
  折り紙に用いるメッシュのクラス
  - CreaseMesh  
  折り目に用いるメッシュのクラス


### **3. Origami_Result**
  - FoldResults  
  メッシュを折る時に必要となるデータを格納する構造体
  - OrigamiFoldResults  
  折り紙メッシュを折る時に必要となるデータを格納する構造体
  - CreaseFoldResults  
  折り目メッシュを折る時に必要となるデータを格納する構造体


### **4. Origami_Utility**
  - OrigamiUtility  
  プロジェクト全般で用いるユーティリティクラス

### **5. Origami_Demo**
   - MeshCreaseDrawer  
   デモ用に作成した、折り目の線を引くクラス

***

### **今後の課題**
  - 折り目の調整
  - 現在はメッシュ単位でデータを管理しているが、メモリを圧迫する原因となるため、頂点など、必要な情報のみを有するクラスに移行する
  - また、現在は折る度に新しいメッシュが生成されるため、ドローコールが次第に増えていくという問題がある。これも上と同じくデータの管理方法を変えることとし、生成されるメッシュの数をなるべく減らすようにする
  - 無駄な計算も減らす
  
### **現在確認されている不具合**  
  - 何度か折っていると折り目のメッシュが回転中にはみ出てしまう
  - 折り紙を折れないと判定された後に線を引き直すと、正常に動作しない
  
