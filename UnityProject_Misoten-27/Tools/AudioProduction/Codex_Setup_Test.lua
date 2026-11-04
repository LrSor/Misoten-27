-- REAPER integration smoke test. Uses only the bundled ReaSynth instrument.
-- Safe to rerun: an existing test track is left unchanged.
local name = "Codex Setup Test"
for i = 0, reaper.CountTracks(0) - 1 do
  local _, existing = reaper.GetSetMediaTrackInfo_String(reaper.GetTrack(0, i), "P_NAME", "", false)
  if existing == name then
    reaper.ShowMessageBox("The test track already exists. Press Play to audition it.", name, 0)
    return
  end
end

reaper.Undo_BeginBlock()
local index = reaper.CountTracks(0)
reaper.InsertTrackAtIndex(index, true)
local track = reaper.GetTrack(0, index)
reaper.GetSetMediaTrackInfo_String(track, "P_NAME", name, true)
reaper.SetMediaTrackInfo_Value(track, "D_VOL", 0.15)
local fx = reaper.TrackFX_AddByName(track, "ReaSynth (Cockos)", false, -1)
if fx < 0 then
  reaper.Undo_EndBlock("Codex setup test (instrument unavailable)", -1)
  reaper.ShowMessageBox("ReaSynth was not found. Open the track FX browser and check the bundled instruments.", name, 0)
  return
end
local item = reaper.CreateNewMIDIItemInProj(track, 0, 4, false)
local take = reaper.GetActiveTake(item)
reaper.GetSetMediaItemTakeInfo_String(take, "P_NAME", "Integration test - C D E G E D C", true)
for i, pitch in ipairs({60, 62, 64, 67, 64, 62, 60}) do
  local t = (i - 1) * 0.5
  reaper.MIDI_InsertNote(take, false, false,
    reaper.MIDI_GetPPQPosFromProjTime(take, t),
    reaper.MIDI_GetPPQPosFromProjTime(take, t + 0.35), 0, pitch, 75, true)
end
reaper.MIDI_Sort(take)
reaper.SetOnlyTrackSelected(track)
reaper.SetEditCurPos(0, false, false)
reaper.TrackList_AdjustWindows(false)
reaper.UpdateArrange()
reaper.Undo_EndBlock("Create Codex setup test", -1)
