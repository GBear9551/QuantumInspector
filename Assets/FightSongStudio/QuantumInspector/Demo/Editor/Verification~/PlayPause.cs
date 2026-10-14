var animator=UnityEngine.Object.FindFirstObjectByType<FightSongStudio.QuantumInspector.Demo.DemoLabAnimator>();
float pausedTime=UnityEditor.SessionState.GetFloat("QI.Demo.Test.PauseTime",-1);
bool pauseHeld=animator.SimulationTime==pausedTime && !animator.IsAnimating;
float cameraError=Vector3.Distance(UnityEngine.Camera.main.transform.position,new Vector3(7,6,-9));
if(!pauseHeld || cameraError>.05f || animator.ViewIndex!=1) throw new System.Exception("Pause/viewpoint integration failed.");
animator.SetAnimationEnabled(true);
UnityEditor.SessionState.SetFloat("QI.Demo.Test.ResumeTime",animator.SimulationTime);
return new {pauseHeld, pausedTime, cameraReachedViewpoint=cameraError<.05f, cameraError, resumed=animator.IsAnimating};
