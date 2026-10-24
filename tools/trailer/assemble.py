# Turns a recorded trailer (fesh-csharp FESH_TRAILER=1: <dir>/frames/f00000.png ... and <dir>/audio.wav) into an MP4.
#   blender -b --python tools/trailer/assemble.py -- <dir> <out.mp4>
# 30 fps, 1280x720, H.264 with AAC sound, through Blender's video sequencer (no ffmpeg needed).
import bpy, os, sys

a = sys.argv[sys.argv.index("--") + 1:]
src, out = a[0], a[1]
frames = sorted(f for f in os.listdir(os.path.join(src, "frames")) if f.endswith(".png"))

scene = bpy.context.scene
scene.render.fps = 30
scene.render.resolution_x, scene.render.resolution_y = 1280, 720
scene.render.resolution_percentage = 100
scene.frame_start, scene.frame_end = 1, len(frames)
ed = scene.sequence_editor_create()
strips = ed.strips if hasattr(ed, "strips") else ed.sequences

img = strips.new_image("frames", os.path.join(src, "frames", frames[0]), 1, 1)
for f in frames[1:]:
    img.elements.append(f)
img.frame_final_duration = len(frames)
try:
    img.transform.filter = 'NEAREST'   # whole pixels: the frames are already 1280x720
except Exception:
    pass
snd = strips.new_sound("audio", os.path.join(src, "audio.wav"), 2, 1)

r = scene.render
try:
    r.image_settings.media_type = 'VIDEO'
except Exception:
    pass
r.image_settings.file_format = 'FFMPEG'
r.ffmpeg.format = 'MPEG4'
r.ffmpeg.codec = 'H264'
r.ffmpeg.constant_rate_factor = 'HIGH'
r.ffmpeg.ffmpeg_preset = 'GOOD'
r.ffmpeg.gopsize = 30
r.ffmpeg.audio_codec = 'AAC'
r.ffmpeg.audio_bitrate = 192
r.ffmpeg.audio_mixrate = 44100
r.ffmpeg.audio_channels = 'STEREO'
r.filepath = out
r.use_file_extension = False
r.use_sequencer = True
bpy.ops.render.render(animation=True)
print("wrote", out, len(frames), "frames")
