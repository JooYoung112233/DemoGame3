const fs=require('fs'),path=require('path'),crypto=require('crypto');
const original=path.resolve('.'),review=path.resolve('../topdown-v11-harmony-review');
const evidence=path.join(original,'검증/화풍조화-교정-v11');
if(original!=='E:\\personalProject\\Demo3\\demo6')throw Error('Unexpected original');
if(fs.existsSync(review))throw Error('Review already exists; preserve it');
fs.mkdirSync(evidence,{recursive:true});
const files=[];
function scan(d){for(const ent of fs.readdirSync(path.join(original,d),{withFileTypes:true})){const p=path.join(d,ent.name);if(ent.isDirectory())scan(p);else files.push({path:p.replaceAll('\\','/'),sha256:crypto.createHash('sha256').update(fs.readFileSync(path.join(original,p))).digest('hex')});}}
for(const d of ['Assets','Packages','ProjectSettings'])scan(d);
fs.writeFileSync(path.join(evidence,'initial-files.json'),JSON.stringify({time:new Date().toISOString(),files},null,2));
fs.mkdirSync(review);
fs.writeFileSync(path.join(review,'.v11-owned-review.json'),JSON.stringify({original,task:'harmony-v11',created:new Date().toISOString()}));
for(const d of ['Assets','Packages','ProjectSettings','UserSettings','Library']){
 if(fs.existsSync(path.join(original,d)))fs.cpSync(path.join(original,d),path.join(review,d),{recursive:true,errorOnExist:true,force:false});
 console.log('Copied '+d);
}
fs.mkdirSync(path.join(original,'아트/화풍조화-교정-v11'),{recursive:true});
console.log(JSON.stringify({review,tracked:files.length}));
