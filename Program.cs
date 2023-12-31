﻿
using System.Drawing;
using System.Numerics;
using static Infinite_module_test.tag_structs;
using static System.Formats.Asn1.AsnWriter;

namespace infinite_proto_terrain_exporter{
    internal class Program{
        static void Main(string[] args){
            //Console.WriteLine("Input source rtrn tag path");
            //string? terrain_tag_path = Console.ReadLine();
            //if (string.IsNullOrEmpty(terrain_tag_path)) throw new Exception("bad input");

            //Console.WriteLine("Input unpacked bitm's folder");
            //string? bitmaps_folder = Console.ReadLine();
            //if (string.IsNullOrEmpty(bitmaps_folder)) throw new Exception("bad input");


            //Console.WriteLine("Input destination folder");
            //string? output_folder = Console.ReadLine();
            //if (string.IsNullOrEmpty(output_folder)) throw new Exception("bad input");

            //Console.WriteLine("beginning conversion process...");
            //convert_images(terrain_tag_path, bitmaps_folder, output_folder);
            //Console.WriteLine("process complete.");
            convert_images("C:\\Users\\Joe bingle\\Downloads\\terrain testing\\forest tag\\rtrn.file", 
                           "C:\\Users\\Joe bingle\\Downloads\\terrain testing\\forest bitmaps", 
                           "C:\\Users\\Joe bingle\\Downloads\\terrain testing\\output");
        }
        static tag load_tag(string file){
            if (!File.Exists(file)) throw new Exception("file does not exist");
            byte[] file_bytes = File.ReadAllBytes(file);

            tag test = new tag(new List<KeyValuePair<byte[], bool>>()); // apparently we do NOT support null, despite declaring it as nullable (this is for compiling at least)
            if (!test.Load_tag_file(file_bytes)) throw new Exception("failed to load tag");
            return test;
        }
        static List<Bitmap> load_bitms(string bitm_folder){
            List<Bitmap> output = new List<Bitmap>();
            var v = Directory.GetFiles(bitm_folder).OrderBy(stringItem => stringItem.Length).ThenBy(stringItem => stringItem).ToList();
            foreach (string filename in v){
                Bitmap current_image = new Bitmap(filename);
                output.Add(current_image);
            }
            return output;
        }


        static void convert_images(string tag_path, string bitm_folder, string out_folder){

            // load all unpacked images into list
            List<Bitmap> bitms = load_bitms(bitm_folder);
            // load rtrn tag
            tag runtime_terrain = load_tag(tag_path);

            // determine the heightmap image's widths
            int heightmaps_width = Convert.ToInt32(runtime_terrain.get_number("Quad Tree LOD Resolution"));
            int othermaps_width = 0; // runtime_terrain.get_number("bitmaps[0].width");

            int lod_level = Convert.ToInt32(runtime_terrain.get_number("Quad Tree Level Count"));
            int lods_per_row = 1 << (lod_level - 1);

            // note that for some reason it doesn't tell us how big the non-heightmap images are, so we have to figuyre those out ourselves
            // determine non-heightmap image widths
            Dictionary<int, bool> multi_widths_test = new();
            foreach (Bitmap bitmap in bitms){
                int current_width = bitmap.Width;
                if (current_width > othermaps_width)
                    othermaps_width = current_width;
                // this is just so that we can confirm that there are only ever 2 different widths, else we'd have to come up with some more junk to support heightmap/nonheightmap widths
                multi_widths_test[current_width] = true;
            }
            if (multi_widths_test.Count > 2)
                throw new Exception("there can only be 2 different sizes of terrain images! time to investigate :(");
            // now we have to round the othermaps width down to the nearest multiple of the heightmaps width
            // as any extra value is purely overlap i think
            othermaps_width = heightmaps_width * (othermaps_width / heightmaps_width);

            // calculate final image sizes
            int final_heightmap_width = heightmaps_width * lods_per_row;
            int final_image_width = othermaps_width * lods_per_row;


            Bitmap?[] out_bitmaps = new Bitmap?[32];
            // init heightmap, aka output index 0
            out_bitmaps[0] = new Bitmap(final_heightmap_width, final_heightmap_width);

            // setup loop system
            var quads_block = runtime_terrain.get_tagblock("Quad Tree Node Data");
            if (quads_block.blocks.Count >= 21845) throw new Exception("terrain has way more quads than this code was designed to process!!");
            for (int i = 0; i < quads_block.blocks.Count; i++){
                // determine what level this is on, how much pixels it occupies
                // and what its offset is

                // for optimization you could make this only evaluate when we move up to the next level, or something like that
                int chunks_per_row;
                int level_local_index;
                if (i < 1){
                    chunks_per_row = 1;
                    level_local_index = 0;
                }else if (i < 5){
                    chunks_per_row = 2;
                    level_local_index = i - 1;
                }else if (i < 21){
                    chunks_per_row = 4;
                    level_local_index = i - 5;
                }else if (i < 85){
                    chunks_per_row = 8;
                    level_local_index = i - 21;
                }else if (i < 341){
                    chunks_per_row = 16;
                    level_local_index = i - 85;
                }else if (i < 1365){
                    chunks_per_row = 32;
                    level_local_index = i - 341;
                }else if (i < 5461){
                    chunks_per_row = 64;
                    level_local_index = i - 1365;
                }else if (i < 21845){
                    chunks_per_row = 128;
                    level_local_index = i - 5461;
                } else throw new Exception("there should never even nearly be this many quads in a terrain");

                int quad_pos_x = level_local_index % chunks_per_row;
                // y actaully gets inverted
                int quad_pos_y = (chunks_per_row - 1) - (level_local_index / chunks_per_row);
                if (quad_pos_y < 0 || quad_pos_y >= chunks_per_row) throw new Exception("somehow exceeded maximum position for lod level!");
                

                var textures_block = runtime_terrain.get_tagblock("Input Texture Data", quads_block.blocks[i], quads_block.GUID);
                for (int texbloc_i = 0; texbloc_i < textures_block.blocks.Count; texbloc_i++){
                    // get the relevant data off this block
                    int output_index = Convert.ToInt32(runtime_terrain.get_number("Output Id", textures_block.blocks[texbloc_i], textures_block.GUID));
                    int bitm_index = Convert.ToInt32(runtime_terrain.get_number("Bitmap Index", textures_block.blocks[texbloc_i], textures_block.GUID));
                    if (bitm_index == 131)
                    {

                    }
                    // if we haven't allocated this image yet, then it may be a good idea to do so
                    if (out_bitmaps[output_index] == null) out_bitmaps[output_index] = new(final_image_width, final_image_width);

                    // now calculate the dimensions that this pixel will occupy
                    int pixel_scale; // total width divided by chunks in row times width of chunk, lowest posssible value should be 1
                    int pixel_x;
                    int pixel_y;
                    if (output_index == 0){ // output for heightmaps
                        pixel_scale = final_heightmap_width / (chunks_per_row * heightmaps_width);
                        pixel_x = quad_pos_x * heightmaps_width * pixel_scale;
                        pixel_y = quad_pos_y * heightmaps_width * pixel_scale;
                    }else {
                        pixel_scale = final_image_width / (chunks_per_row * othermaps_width);
                        pixel_x = quad_pos_x * othermaps_width * pixel_scale;
                        pixel_y = quad_pos_y * othermaps_width * pixel_scale;
                    }
                    // test to make sure we didn't screw up the maths here
                    if (pixel_scale < 1) throw new Exception("failed to calculate correct pixel size?");

                    // and now we can paste in the pixels
                    Bitmap destination_bitmap = out_bitmaps[output_index];
                    Bitmap source_bitmap = bitms[bitm_index];
                    for (int x = 0; x < source_bitmap.Width; x++){
                        for (int y = 0; y < source_bitmap.Height; y++){

                            var pixel = source_bitmap.GetPixel(x,y);
                            int dest_x = (x * pixel_scale) + pixel_x;
                            int dest_y = (y * pixel_scale) + pixel_y;
                            // factor in pixel scale
                            for (int scale_x = 0; scale_x < pixel_scale; scale_x++){
                                // make sure this stays inside the image frame (some images are slightly oversized?)
                                if (dest_x + scale_x >= destination_bitmap.Width) continue;

                                for (int scale_y = 0; scale_y < pixel_scale; scale_y++){
                                    // again make sure pixel is inside the final image
                                    if (dest_y + scale_y >= destination_bitmap.Height) continue;
                                    // finally set the pixel
                                    destination_bitmap.SetPixel(dest_x + scale_x, dest_y + scale_y, pixel);
                                }
                            }

                        }
                    }
                }

            }

            // alright, spit out all the images now
            for (int i = 0; i < out_bitmaps.Length; i++)
                if (out_bitmaps[i] != null)
                    out_bitmaps[i].Save(out_folder + "\\img_" + i + ".png");


            // and now that all the images are sorted out, we want to run through the heightmap and bake out the geometry for it

            // get basic information
            Vector3 position = runtime_terrain.get_float3("Quad Tree Position");
            Vector3 scale = runtime_terrain.get_float3("Quad Tree Size");

            // for now we're going to make each pixel into a quad, but we'll smooth it out later when we confirm the best method
            Bitmap? heightmap = out_bitmaps[0];
            if (heightmap == null) throw new Exception("cant export heightmap if there is no heightmap");
            

            // setup vert maps
            int[,] pixel_indicies = new int[heightmap.Width, heightmap.Height];
            for (int x = 0; x < heightmap.Width; x++)
                for (int y = 0; y < heightmap.Height; y++)
                    pixel_indicies[x, y] = -1;

            List<Vector3> vertices = new();
            List<Vector2> UVs = new();
            List<tri> indices = new();
            // ignore the last indexes as we're only trying to draw quads, so we only need to use the last indicies on the index prior
            for (int x = 0; x < heightmap.Width-1; x++){
                for (int y = 0; y < heightmap.Height-1; y++){


                    // return height, pos & UV for [i..i+1][i..i+1] verts (4 verts)
                    // v1 v2
                    // v3 v4
                    vert v1 = get_vertex(heightmap, x, y, scale, position);
                    vert v2 = get_vertex(heightmap, x+1, y, scale, position);
                    vert v3 = get_vertex(heightmap, x, y+1, scale, position);
                    vert v4 = get_vertex(heightmap, x+1, y+1, scale, position);

                    // if 1,2,3 are valid, then the 1st triangle is valid
                    if (v1.height != null && v2.height != null && v3.height != null){
                        tri tri = new tri();
                        // check each one to see if we've added it before
                        tri.v1 = get_or_set_vert(x  ,y  , v1, pixel_indicies, vertices, UVs);
                        tri.v2 = get_or_set_vert(x+1,y  , v2, pixel_indicies, vertices, UVs);
                        tri.v3 = get_or_set_vert(x,  y+1, v3, pixel_indicies, vertices, UVs);
                        indices.Add(tri);
                    }
                    // if 2,3,4 are valid, then the 2nd triangle is valid
                    if (v2.height != null && v3.height != null && v4.height != null){
                        tri tri = new tri();
                        // check each one to see if we've added it before
                        tri.v3 = get_or_set_vert(x+1, y  , v2, pixel_indicies, vertices, UVs);
                        tri.v2 = get_or_set_vert(x  , y+1, v3, pixel_indicies, vertices, UVs);
                        tri.v1 = get_or_set_vert(x+1, y+1, v4, pixel_indicies, vertices, UVs);
                        indices.Add(tri);
                    }



                }
            }

            // now convert to obj file
            using (StreamWriter writer = File.CreateText(out_folder + "\\mesh.obj")){
                writer.WriteLine("o Terrain");
                // write verts
                foreach (var v in vertices)
                    writer.WriteLine("v " + v.X + " " + v.Y + " " + v.Z);
                // write UVs
                foreach (var v in UVs)
                    writer.WriteLine("vt " + v.X + " " + v.Y);
                // shading off
                writer.WriteLine("s 0");

                // write indices (obj starts from 1 not 0)
                foreach (var v in indices)
                    writer.WriteLine("f " + (v.v1+1) + "/" + (v.v1+1) + " " +
                                          + (v.v2+1) + "/" + (v.v2+1) + " " +
                                          + (v.v3+1) + "/" + (v.v3+1));
            }

        }
        struct tri{
            //public tri(int _1, int _2, int _3){
            //    v1 = _1; v2 = _2; v3 = _3;}
            public int v1;
            public int v2;
            public int v3;
        }
        struct vert{
            public vert(Vector2 _pos, Vector2 _UV, float? _height) {
                pos = _pos; UV = _UV; height = _height;}
            public Vector2 pos;
            public Vector2 UV;
            public float? height;
        }
        static vert get_vertex(Bitmap? heightmap, int x, int y, Vector3 scale, Vector3 position){
            // get height value
            var pixel = heightmap.GetPixel(x, y);
            float? height = ((pixel.R / 255.0f) * scale.Z);

            if (height <= 0.0f) height = null;
            else height += position.Z; 
                                  

            float normalized_x = (float)x / heightmap.Width;
            float normalized_y = (float)y / heightmap.Height;

            // add the world position vertices
            float world_x = (normalized_x * scale.X) + position.X;
            float world_y = (normalized_y * scale.Y) + position.Y;

            return new(new(world_x, world_y), new(normalized_x, normalized_y), height);
        }

        static int get_or_set_vert(int x, int y, vert v, int[,] pixel_indicies, List<Vector3> vertices, List<Vector2> UVs){
            if (pixel_indicies[x,y] == -1){ // v3
                int index = vertices.Count;
                pixel_indicies[x,y] = index;
                vertices.Add(new(v.pos.X, v.pos.Y, (float)v.height));
                UVs.Add(new(v.UV.X, v.UV.Y));
                return index;
            } else return pixel_indicies[x, y];
        }
    }
}