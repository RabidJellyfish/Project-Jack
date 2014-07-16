﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using FarseerPhysics;
using FarseerPhysics.Common;
using FarseerPhysics.Dynamics;
using FarseerPhysics.Factories;

namespace Badminton
{
	class Wall
	{
		private Body body;

		private float width, height;
		public float Width { get { return width; } }
		public float Height { get { return height; } }

		public Wall()
		{
		}

		public Wall(World w, float x, float y, float width, float height, float rotation)
		{
			this.width = width * MainGame.PIXEL_TO_METER;
			this.height = height * MainGame.PIXEL_TO_METER;
			body = BodyFactory.CreateRectangle(w, this.width, this.height, 1f);
			body.Friction = 100f;
			body.Rotation = rotation;
			body.Position = new Vector2(x, y) * MainGame.PIXEL_TO_METER;
			body.BodyType = BodyType.Static;
			body.CollisionCategories = Category.Cat31; // change when more players are added
			body.UserData = this;
		}

		public void Draw(SpriteBatch sb)
		{
			// change texture when stuff, maybe add as parameter
			sb.Draw(MainGame.tex_box, new Rectangle((int)(body.Position.X * MainGame.METER_TO_PIXEL),
										(int)(body.Position.Y * MainGame.METER_TO_PIXEL),
										(int)(width * MainGame.METER_TO_PIXEL), 
										(int)(height * MainGame.METER_TO_PIXEL)), 
					null, Color.White, body.Rotation, new Vector2(16, 16), SpriteEffects.None, 0.0f);
		}
	}
}
