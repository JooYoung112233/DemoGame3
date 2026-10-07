const fs=require('fs');let s=fs.readFileSync('AgentScripts/V054GuardObserve.cs','utf8');
s=s.replaceAll('V054GuardObserver','V054FinalGuardObserver').replaceAll('V054GuardObserve','V054FinalGuardObserve').replaceAll('V054 guard read-only observer','V054 final guard read-only observer');
s=s.replace('if(p.InWeaponAct||changed)','if(p.InWeaponAct && p.ActKind.ToString()=="Guard" || (prior!=null && prior.StartsWith("Lower/") && changed))');
s=s.replace('prior=key;}','prior=key;if(changed)Flush();}').replace('observed-','native-final-');
s=s.replace('void OnDestroy(){InputSystem.onEvent-=OnInput;}','void Flush(){File.WriteAllText(Out+"/native-final.json",JsonConvert.SerializeObject(new{utc=DateTime.UtcNow.ToString("O"),method="Passive native mouse + rendered state observation; no injected input",events,pictures,trace},Formatting.Indented));} void OnDestroy(){InputSystem.onEvent-=OnInput;Flush();}');
fs.writeFileSync('AgentScripts/V054FinalGuardObserve.cs',s);
