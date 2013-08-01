ExposureMeter
=============

WIP: Exposure / Light Meter app for Windows Phone 8
---------------------------------------------------

This code is still very much in the experimental phase.
I'm still trying to figure out if it's even feasible to implement a light meter in Windows Phone.

As a result, the code is *ugly*!
I'm going to do stuff like put everything in one codebehind class, and have all kinds of unused code lying around.
Once I have the basics working, I'll refactor everything into a proper layout.

Challenges:

* **Phone cameras don't have an adjustible aperture!**
  My Lumia 920, for example, is fixed at f/2.
  It appears that it adjusts light levels by varying just the ISO level and the shutter speed.

  Normal cameras have 3 parameters: ISO, shutter speed, and aperture. Phone cameras merge ISO and aperture into one value, where one is derived from the other; whichever is unbound by the user.
  
* **How do I figure out what the fixed aperture value *is***?
  It varies depending on the model of phone, and there doesn't seem to be an API for getting at it.
  Best I can think of at this point is just taking a picture and looking in its EXIF data.
