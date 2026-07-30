This is the Phantom Weight project.

## Error Log ##
Errors that we have solved in the pass, the controller not appearing and the controller inputs are not being detected by the meta XR interaction kit. Assets not loading into unity (they appear purple or with max brightness). We also fixed when the hand and controller are connecting at the same time model blend issue where we just did individual states instead of combining them. We also fixed the physical and height move in real life and moving like 3cm by getting the change distance and then scaling with a conversion equation. For this problem "Make the objects smaller because when the person picks up the objects, it looks way to big compared to when the person is far apart (I know that this might be common sense but it's different from our environment which makes the experience less realistic)" by adding grab transformer scripts that constraint size to our grabbable objects.

Things that we have to do right now : 
- Make the map solid and the blocks solid **(Prince)** (adding box colliders right now just either clips the camera into the ground or other space issues appear)
- Add gravity for the environment and fix the pass through
- Make run and jump controller buttons
- Camera going through a solid object just appears as a grey screen
- Make the calibration screen
  

## Personal Weight Formula ##
<img width="461" height="824" alt="image" src="https://github.com/user-attachments/assets/ab58254c-f175-49d1-befd-b85038623c12" />



## Hardware coding framework ##
1. Player picks up item -> Prints out weight assigned to item -> Converts weight to # of button presses through formula in a script
2. Send # of number of button presses to ESP32 -> through terminal -> EMS how many times to write to OP
3. When player releases item -> check if grabbing or not -> if not then loop all the way to 0 (need global currentWeight variable which solves dropping and picking up really fast)


## Starting out in Unity ##
1. ** The first part of the software side of this project is the unity environment **
In the unity environment, you can download the official unity installer at their website. For the environment in this project, you can make your own room or import a room from unity assets. Then make sure that you install Meta all in one SDK into you unity project so you get the meta Building blocks. 

2. First drag the camera rig into your environment.

3. Connect the camera and the controllers into the environments

4. Add the Loco move script to the camera rig so the player can move and control the player in the game using controllers

5. Add cubes or imported assets and then add the meta block grabbable script to it.

