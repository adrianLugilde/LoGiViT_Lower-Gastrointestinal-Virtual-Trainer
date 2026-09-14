import bpy
import sys

# Get input and output paths from command line arguments
input_path = sys.argv[-2]
output_path = sys.argv[-1]

# Clear existing mesh objects
bpy.ops.object.select_all(action='DESELECT')
bpy.ops.object.select_by_type(type='MESH')
bpy.ops.object.delete()

# Import FBX
bpy.ops.import_scene.fbx(filepath=input_path)

# Get the imported object
obj = bpy.context.selected_objects[0]

# Recalculate UVs using Smart UV Project
bpy.context.view_layer.objects.active = obj
bpy.ops.object.mode_set(mode='EDIT')
bpy.ops.uv.smart_project()
bpy.ops.object.mode_set(mode='OBJECT')

# Export the FBX with new UVs
bpy.ops.export_scene.fbx(filepath=output_path, use_selection=True)
