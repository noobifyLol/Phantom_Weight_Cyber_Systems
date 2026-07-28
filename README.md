This is the Phantom Weight project.

## Error Log ##
Errors that we have solved in the pass, the controller not appearing and the controller inputs are not being detected by the meta XR interaction kit. Assets not loading into unity (they appear purple or with max brightness).

Things that we have to do right now : 
- Make the headset responsive to it's relative height **(Dat)**
- Make the map solid and the blocks solid **(Prince)**
- Add gravity for the environment and fix the pass through (if we make the headset responsive height we don't have to worry about adding jump or grabvity for the camera which also fixes the passthroguht problem) 
- Fix the jump button and button inputs and make a run function
- Fix the model for when controllers and using realistic hand simultaneously since they both appear. 
- Make the objects smallers because when the person pciks up the objects, it looks way to big comapred to when the person is far apart (I know that this might be common sense but it's different from out environment which makes the experience less realistic) 
- hand and controller connecting at the same time model blend issue **(Dat)**


## Starting out in Unity ##
1. ** The first part of the software side of this project is the unity environment **
In the unity environment, you can download the official unity installer at their website. For the environment in this project, you can make your own room or import a room from unity assets. Then make sure that you install Meta all in one SDK into you unity project so you get the meta Building blocks. 


2. First drag the camera rig into your environment.

3. Connect the camera and the controllers into the environemtns

4. Add the Locomove script to the camera rig so the player can move and control the player in the game using controllers

5. Add cubes or imported assets and then add the meta blcok grabbable script to it.

