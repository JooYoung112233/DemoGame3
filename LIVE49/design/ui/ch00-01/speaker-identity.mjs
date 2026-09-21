// Author identities remain stable; player-visible names require an explicit reveal.
export function speakerNamesAt(data,index){
 const known={...data.names_known_on_entry};
 for(const reveal of data.name_reveals??[]){
  const trigger=data.lines.findIndex(item=>item.id===reveal.after_line);
  if(trigger>=0&&trigger<index)known[reveal.speaker_id]=reveal.display_name;
 }
 return known;
}
export function speakerLabel(data,index){
 const line=data.lines[index];if(!line.speaker)return '';
 return speakerNamesAt(data,index)[line.speaker_id]??data.speaker_identity?.unknown_label??'?';
}
