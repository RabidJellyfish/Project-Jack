﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using FarseerPhysics;
using FarseerPhysics.Common;
using FarseerPhysics.Dynamics;
using FarseerPhysics.Dynamics.Joints;
using FarseerPhysics.Dynamics.Contacts;
using FarseerPhysics.Factories;

using Badminton.Attacks;

namespace Badminton.Stick_Figures
{
	class StickFigure
	{
		private World world;

		// Ragdoll
		protected Body torso, head, leftUpperArm, rightUpperArm, leftLowerArm, rightLowerArm, leftUpperLeg, rightUpperLeg, leftLowerLeg, rightLowerLeg;
		protected Body gyro;
		private AngleJoint neck, leftShoulder, rightShoulder, leftElbow, rightElbow, leftHip, rightHip, leftKnee, rightKnee;
		private RevoluteJoint r_neck, r_leftShoulder, r_rightShoulder, r_leftElbow, r_rightElbow, r_leftHip, r_rightHip, r_leftKnee, r_rightKnee;
		private AngleJoint upright;

		// Limb control
		public Dictionary<Body, float> health;
		private float maxImpulse;
		private float friction;

		private List<Attack> attacks;

		// Position properties
		/// <summary>
		/// The torso's position
		/// </summary>
		public Vector2 Position { get { return torso.Position; } }

		/// <summary>
		/// The left hand's position
		/// </summary>
		public Vector2 LeftHandPosition
		{
			get
			{
				if (health[leftLowerArm] > 0)
					return leftLowerArm.Position + new Vector2((float)Math.Sin(leftLowerArm.Rotation), -(float)Math.Cos(leftLowerArm.Rotation)) * 7.5f * scale * MainGame.PIXEL_TO_METER;
				else
					return -Vector2.One;
			}
		}
		
		/// <summary>
		/// The right hand's position
		/// </summary>
		public Vector2 RightHandPosition
		{
			get
			{
				if (health[rightLowerArm] > 0)
					return rightLowerArm.Position + new Vector2((float)Math.Sin(rightLowerArm.Rotation), -(float)Math.Cos(rightLowerArm.Rotation)) * 7.5f * scale * MainGame.PIXEL_TO_METER;
				else
					return -Vector2.One;
			}
		}

		/// <summary>
		/// The left foot's position
		/// </summary>
		public Vector2 LeftFootPosition
		{
			get
			{
				if (health[leftLowerLeg] > 0)
					return leftLowerLeg.Position - new Vector2((float)Math.Sin(leftLowerLeg.Rotation), -(float)Math.Cos(leftLowerLeg.Rotation)) * 7.5f * scale * MainGame.PIXEL_TO_METER;
				else
					return -Vector2.One;
			}
		}

		/// <summary>
		/// The right foot's position
		/// </summary>
		public Vector2 RightFootPosition
		{
			get
			{
				if (health[rightLowerLeg] > 0)
					return rightLowerLeg.Position - new Vector2((float)Math.Sin(rightLowerLeg.Rotation), -(float)Math.Cos(rightLowerLeg.Rotation)) * 7.5f * scale * MainGame.PIXEL_TO_METER;
				else
					return -Vector2.One;
			}
		}

		/// <summary>
		/// Whether or not the figure is dead
		/// </summary>
		public bool IsDead
		{
			get { return health[head] <= 0 || health[torso] <= 0; }
		}

		// Action flags
		public bool Crouching { get; set; }
		private int walkStage;

		private float attackAngle;
		private bool punching, kicking, aiming;
		private bool punchArm, kickLeg; // true=left, false=right
		private int punchStage, kickStage, chargeUp, coolDown;
		private const int MAX_CHARGE = 100, COOL_PERIOD = 30;

		// Other
		private float scale;
		private Color color;
		private bool onGround;
		private int groundCheck;
		private Category collisionCat;
		protected bool LastFacedLeft { get; set; }

		#region Creation/Destruction

		/// <summary>
		/// Stick figure constructor
		/// </summary>
		/// <param name="world">The world to place the physics objects in</param>
		/// <param name="position">The position to place the center of the stick figure's torso</param>
		/// <param name="collisionCat">The collision category of the figure. Different players will have different collision categories.</param>
		/// <param name="c">The color of the stick figure</param>
		public StickFigure(World world, Vector2 position, Category collisionCat, float scale, Color c)
		{
			this.world = world;
			maxImpulse = 0.2f * scale * scale;
			friction = 5f * scale;
			Crouching = false;
			health = new Dictionary<Body, float>();
			this.color = c;
			this.scale = scale;

			walkStage = 0;

			attackAngle = 0f;
			punching = kicking = false;
			punchArm = kickLeg = false;
			punchStage = kickStage = -1;
			chargeUp = 0;
			attacks = new List<Attack>();

			groundCheck = 0;
			this.collisionCat = collisionCat;
			LastFacedLeft = true;

			GenerateBody(world, position, collisionCat);
			ConnectBody(world);

			// TODO: Replace with ray tracing for more consistent ground detection
			onGround = false;
			leftLowerLeg.OnCollision += new OnCollisionEventHandler(OnGroundCollision);
			rightLowerLeg.OnCollision += new OnCollisionEventHandler(OnGroundCollision);
			leftLowerLeg.OnSeparation += new OnSeparationEventHandler(OnGroundSeparation);
			rightLowerLeg.OnSeparation += new OnSeparationEventHandler(OnGroundSeparation);

			head.OnCollision += new OnCollisionEventHandler(DamageCollisions);
			leftUpperArm.OnCollision += new OnCollisionEventHandler(DamageCollisions);
			leftLowerArm.OnCollision += new OnCollisionEventHandler(DamageCollisions);
			rightUpperArm.OnCollision += new OnCollisionEventHandler(DamageCollisions);
			rightLowerArm.OnCollision += new OnCollisionEventHandler(DamageCollisions);
			leftUpperLeg.OnCollision += new OnCollisionEventHandler(DamageCollisions);
			leftLowerLeg.OnCollision += new OnCollisionEventHandler(DamageCollisions);
			rightUpperLeg.OnCollision += new OnCollisionEventHandler(DamageCollisions);
			rightLowerLeg.OnCollision += new OnCollisionEventHandler(DamageCollisions);

			Stand();
		}

		/// <summary>
		/// Generates the stick figure's limbs, torso, and head
		/// </summary>
		/// <param name="world">The physics world to add the bodies to</param>
		/// <param name="position">The position to place the center of the torso</param>
		/// <param name="collisionCat">The collision category of the stick figure</param>
		protected void GenerateBody(World world, Vector2 position, Category collisionCat)
		{
			torso = BodyFactory.CreateCapsule(world, 40 * scale * MainGame.PIXEL_TO_METER, 5 * scale * MainGame.PIXEL_TO_METER, 10.0f);
			torso.Position = position;
			torso.BodyType = BodyType.Dynamic;
			torso.CollisionCategories = collisionCat;
			torso.CollidesWith = Category.All & ~collisionCat;
			gyro = BodyFactory.CreateBody(world, torso.Position);
			gyro.CollidesWith = Category.None;
			gyro.BodyType = BodyType.Dynamic;
			gyro.Mass = 0.00001f;
			gyro.FixedRotation = true;
			health.Add(torso, 1.0f);

			head = BodyFactory.CreateCircle(world, 12.5f * scale * MainGame.PIXEL_TO_METER, 1.0f);
			head.Position = torso.Position - new Vector2(0, 29f) * scale * MainGame.PIXEL_TO_METER;
			head.BodyType = BodyType.Dynamic;
			head.CollisionCategories = collisionCat;
			head.CollidesWith = Category.All & ~collisionCat;
			head.Restitution = 0.2f;
			health.Add(head, 1.0f);

			leftUpperArm = BodyFactory.CreateCapsule(world, 25 * scale * MainGame.PIXEL_TO_METER, 5 * scale * MainGame.PIXEL_TO_METER, 0.1f);
			leftUpperArm.Rotation = -MathHelper.PiOver2;
			leftUpperArm.Position = torso.Position + new Vector2(-7.5f, -15) * scale * MainGame.PIXEL_TO_METER;
			leftUpperArm.BodyType = BodyType.Dynamic;
			leftUpperArm.CollisionCategories = collisionCat;
			leftUpperArm.CollidesWith = Category.All & ~collisionCat;
			health.Add(leftUpperArm, 1.0f);

			rightUpperArm = BodyFactory.CreateCapsule(world, 25 * scale * MainGame.PIXEL_TO_METER, 5 * scale * MainGame.PIXEL_TO_METER, 0.1f);
			rightUpperArm.Rotation = MathHelper.PiOver2;
			rightUpperArm.Position = torso.Position + new Vector2(7.5f, -15) * scale * MainGame.PIXEL_TO_METER;
			rightUpperArm.BodyType = BodyType.Dynamic;
			rightUpperArm.CollisionCategories = collisionCat;
			rightUpperArm.CollidesWith = Category.All & ~collisionCat;
			health.Add(rightUpperArm, 1.0f);

			leftLowerArm = BodyFactory.CreateCapsule(world, 25 * scale * MainGame.PIXEL_TO_METER, 5 * scale * MainGame.PIXEL_TO_METER, 0.1f);
			leftLowerArm.Rotation = -MathHelper.PiOver2;
			leftLowerArm.Position = torso.Position + new Vector2(-22.5f, -15) * scale * MainGame.PIXEL_TO_METER;
			leftLowerArm.BodyType = BodyType.Dynamic;
			leftLowerArm.CollisionCategories = collisionCat;
			leftLowerArm.CollidesWith = Category.All & ~collisionCat;
			health.Add(leftLowerArm, 1.0f);

			rightLowerArm = BodyFactory.CreateCapsule(world, 25 * scale * MainGame.PIXEL_TO_METER, 5 * scale * MainGame.PIXEL_TO_METER, 0.1f);
			rightLowerArm.Rotation = MathHelper.PiOver2;
			rightLowerArm.Position = torso.Position + new Vector2(22.5f, -15) * scale * MainGame.PIXEL_TO_METER;
			rightLowerArm.BodyType = BodyType.Dynamic;
			rightLowerArm.CollisionCategories = collisionCat;
			rightLowerArm.CollidesWith = Category.All & ~collisionCat;
			health.Add(rightLowerArm, 1.0f);

			leftUpperLeg = BodyFactory.CreateCapsule(world, 25 * scale * MainGame.PIXEL_TO_METER, 5 * scale * MainGame.PIXEL_TO_METER, 5f);
			leftUpperLeg.Rotation = -3 * MathHelper.PiOver4;
			leftUpperLeg.Position = torso.Position + new Vector2(-25f / (float)Math.Sqrt(8) + 4, 10 + 25f / (float)Math.Sqrt(8)) * scale * MainGame.PIXEL_TO_METER;
			leftUpperLeg.BodyType = BodyType.Dynamic;
			leftUpperLeg.CollisionCategories = collisionCat;
			leftUpperLeg.CollidesWith = Category.All & ~collisionCat;
			leftUpperLeg.Restitution = 0.15f;
			health.Add(leftUpperLeg, 1.0f);

			rightUpperLeg = BodyFactory.CreateCapsule(world, 25 * scale * MainGame.PIXEL_TO_METER, 5 * scale * MainGame.PIXEL_TO_METER, 5f);
			rightUpperLeg.Rotation = 3 * MathHelper.PiOver4;
			rightUpperLeg.Position = torso.Position + new Vector2(25f / (float)Math.Sqrt(8) - 4, 10 + 25f / (float)Math.Sqrt(8)) * scale * MainGame.PIXEL_TO_METER;
			rightUpperLeg.BodyType = BodyType.Dynamic;
			rightUpperLeg.CollisionCategories = collisionCat;
			rightUpperLeg.CollidesWith = Category.All & ~collisionCat;
			rightUpperLeg.Restitution = 0.15f;
			health.Add(rightUpperLeg, 1.0f);

			leftLowerLeg = BodyFactory.CreateCapsule(world, 25 * scale * MainGame.PIXEL_TO_METER, 5 * scale * MainGame.PIXEL_TO_METER, 10.0f);
			leftLowerLeg.Position = torso.Position + new Vector2(-50f / (float)Math.Sqrt(8) + 6, 25 + 25f / (float)Math.Sqrt(8)) * scale * MainGame.PIXEL_TO_METER;
			leftLowerLeg.BodyType = BodyType.Dynamic;
			leftLowerLeg.CollisionCategories = collisionCat;
			leftLowerLeg.CollidesWith = Category.All & ~collisionCat;
			leftLowerLeg.Restitution = 0.15f;
			leftLowerLeg.Friction = friction;
			health.Add(leftLowerLeg, 1.0f);

			rightLowerLeg = BodyFactory.CreateCapsule(world, 25 * scale * MainGame.PIXEL_TO_METER, 5 * scale * MainGame.PIXEL_TO_METER, 10.0f);
			rightLowerLeg.Position = torso.Position + new Vector2(50f / (float)Math.Sqrt(8) - 6, 25 + 25f / (float)Math.Sqrt(8)) * scale * MainGame.PIXEL_TO_METER;
			rightLowerLeg.BodyType = BodyType.Dynamic;
			rightLowerLeg.CollisionCategories = collisionCat;
			rightLowerLeg.CollidesWith = Category.All & ~collisionCat;
			rightLowerLeg.Restitution = 0.15f;
			rightLowerLeg.Friction = friction;
			health.Add(rightLowerLeg, 1.0f);
		}

		/// <summary>
		/// Connects the figure's body parts
		/// </summary>
		/// <param name="world">The physics world to add the joints to</param>
		protected void ConnectBody(World world)
		{
			upright = JointFactory.CreateAngleJoint(world, torso, gyro);
			upright.MaxImpulse = maxImpulse * 0.2f;
			upright.TargetAngle = 0.0f;
			upright.CollideConnected = false;

			r_neck = JointFactory.CreateRevoluteJoint(world, head, torso, -Vector2.UnitY * 20 * scale * MainGame.PIXEL_TO_METER);
			neck = JointFactory.CreateAngleJoint(world, head, torso);
			neck.CollideConnected = false;
			neck.MaxImpulse = maxImpulse;

			r_leftShoulder = JointFactory.CreateRevoluteJoint(world, leftUpperArm, torso, -Vector2.UnitY * 15 * scale * MainGame.PIXEL_TO_METER);
			leftShoulder = JointFactory.CreateAngleJoint(world, leftUpperArm, torso);
			leftShoulder.CollideConnected = false;
			leftShoulder.MaxImpulse = maxImpulse;

			r_rightShoulder = JointFactory.CreateRevoluteJoint(world, rightUpperArm, torso, -Vector2.UnitY * 15 * scale * MainGame.PIXEL_TO_METER);
			rightShoulder = JointFactory.CreateAngleJoint(world, rightUpperArm, torso);
			rightShoulder.CollideConnected = false;
			rightShoulder.MaxImpulse = maxImpulse;

			r_leftElbow = JointFactory.CreateRevoluteJoint(world, leftLowerArm, leftUpperArm, -Vector2.UnitY * 7.5f * scale * MainGame.PIXEL_TO_METER);
			leftElbow = JointFactory.CreateAngleJoint(world, leftLowerArm, leftUpperArm);
			leftElbow.CollideConnected = false;
			leftElbow.MaxImpulse = maxImpulse;

			r_rightElbow = JointFactory.CreateRevoluteJoint(world, rightLowerArm, rightUpperArm, -Vector2.UnitY * 7.5f * scale * MainGame.PIXEL_TO_METER);
			rightElbow = JointFactory.CreateAngleJoint(world, rightLowerArm, rightUpperArm);
			rightElbow.CollideConnected = false;
			rightElbow.MaxImpulse = maxImpulse;

			r_leftHip = JointFactory.CreateRevoluteJoint(world, leftUpperLeg, torso, Vector2.UnitY * 15 * scale * MainGame.PIXEL_TO_METER);
			leftHip = JointFactory.CreateAngleJoint(world, leftUpperLeg, torso);
			leftHip.CollideConnected = false;
			leftHip.MaxImpulse = maxImpulse;

			r_rightHip = JointFactory.CreateRevoluteJoint(world, rightUpperLeg, torso, Vector2.UnitY * 15 * scale * MainGame.PIXEL_TO_METER);
			rightHip = JointFactory.CreateAngleJoint(world, rightUpperLeg, torso);
			rightHip.CollideConnected = false;
			rightHip.MaxImpulse = maxImpulse;

			r_leftKnee = JointFactory.CreateRevoluteJoint(world, leftLowerLeg, leftUpperLeg, -Vector2.UnitY * 7.5f * scale * MainGame.PIXEL_TO_METER);
			leftKnee = JointFactory.CreateAngleJoint(world, leftUpperLeg, leftLowerLeg);
			leftKnee.CollideConnected = false;
			leftKnee.MaxImpulse = maxImpulse;

			r_rightKnee = JointFactory.CreateRevoluteJoint(world, rightLowerLeg, rightUpperLeg, -Vector2.UnitY * 7.5f * scale * MainGame.PIXEL_TO_METER);
			rightKnee = JointFactory.CreateAngleJoint(world, rightUpperLeg, rightLowerLeg);
			rightKnee.CollideConnected = false;
			rightKnee.MaxImpulse = maxImpulse;
		}

		/// <summary>
		/// Removes all limbs and joints from the physics world. Call this before respawning
		/// </summary>
		public void Destroy()
		{
			// Remove joints
			if (world.JointList.Contains(neck))
				world.RemoveJoint(neck);
			if (world.JointList.Contains(r_neck))
				world.RemoveJoint(r_neck);
			if (world.JointList.Contains(leftShoulder))
				world.RemoveJoint(leftShoulder);
			if (world.JointList.Contains(r_leftShoulder))
				world.RemoveJoint(r_leftShoulder);
			if (world.JointList.Contains(leftElbow))
				world.RemoveJoint(leftElbow);
			if (world.JointList.Contains(r_leftElbow))
				world.RemoveJoint(r_leftElbow);
			if (world.JointList.Contains(rightShoulder))
				world.RemoveJoint(rightShoulder);
			if (world.JointList.Contains(r_rightShoulder))
				world.RemoveJoint(r_rightShoulder);
			if (world.JointList.Contains(rightElbow))
				world.RemoveJoint(rightElbow);
			if (world.JointList.Contains(r_rightElbow))
				world.RemoveJoint(r_rightElbow);
			if (world.JointList.Contains(leftHip))
				world.RemoveJoint(leftHip);
			if (world.JointList.Contains(r_leftHip))
				world.RemoveJoint(r_leftHip);
			if (world.JointList.Contains(leftKnee))
				world.RemoveJoint(leftKnee);
			if (world.JointList.Contains(r_leftKnee))
				world.RemoveJoint(r_leftKnee);
			if (world.JointList.Contains(rightHip))
				world.RemoveJoint(rightHip);
			if (world.JointList.Contains(r_rightHip))
				world.RemoveJoint(r_rightHip);
			if (world.JointList.Contains(rightKnee))
				world.RemoveJoint(rightKnee);
			if (world.JointList.Contains(r_rightKnee))
				world.RemoveJoint(r_rightKnee);
			if (world.JointList.Contains(upright))
				world.RemoveJoint(upright);

			// Remove limbs
			if (world.BodyList.Contains(head))
				world.RemoveBody(head);
			if (world.BodyList.Contains(torso))
				world.RemoveBody(torso);
			if (world.BodyList.Contains(leftUpperArm))
				world.RemoveBody(leftUpperArm);
			if (world.BodyList.Contains(leftLowerArm))
				world.RemoveBody(leftLowerArm);
			if (world.BodyList.Contains(rightUpperArm))
				world.RemoveBody(rightUpperArm);
			if (world.BodyList.Contains(rightLowerArm))
				world.RemoveBody(rightLowerArm);
			if (world.BodyList.Contains(leftUpperLeg))
				world.RemoveBody(leftUpperLeg);
			if (world.BodyList.Contains(leftLowerLeg))
				world.RemoveBody(leftLowerLeg);
			if (world.BodyList.Contains(rightUpperLeg))
				world.RemoveBody(rightUpperLeg);
			if (world.BodyList.Contains(rightLowerLeg))
				world.RemoveBody(rightLowerLeg);
			if (world.BodyList.Contains(gyro))
				world.RemoveBody(gyro);
		}

		#endregion

		#region Collision handlers

		private bool OnGroundCollision(Fixture fixtureA, Fixture fixtureB, Contact contact)
		{
			if (fixtureB.Body.UserData is Wall)
			{
				Vector2 normal = contact.Manifold.LocalNormal;
				if (normal.X == 0 || normal.Y / normal.X > 1)
					onGround = true;
			}
			return contact.IsTouching;
		}
		private void OnGroundSeparation(Fixture fixtureA, Fixture fixtureB)
		{
			onGround = false;
		}

		private bool DamageCollisions(Fixture fixtureA, Fixture fixtureB, Contact contact)
		{
			if (fixtureB.Body.UserData is ForceWave)
			{
				ForceWave f = (ForceWave)fixtureB.Body.UserData;
				fixtureB.Body.UserData = null;

				health[fixtureA.Body] -= f.Damage;

				Random r = new Random();
				int index = r.Next(MainGame.sfx_punches.Length);
				MainGame.sfx_punches[index].Play();
			}
			else if (fixtureB.Body.UserData is LongRangeAttack)
			{
				LongRangeAttack f = (LongRangeAttack)fixtureB.Body.UserData;
				fixtureB.Body.UserData = null;

				health[fixtureA.Body] -= f.Damage;

				// TODO: Play sound
			}

			return contact.IsTouching;
		}

		#endregion

		#region Actions

		/// <summary>
		/// Sends the stick figure to its default pose
		/// </summary>
		public void Stand()
		{
			Crouching = false;
			upright.TargetAngle = 0.0f;
			walkStage = 0;
			if (!kicking || kicking && !kickLeg)
			{
				leftHip.TargetAngle = 3 * MathHelper.PiOver4;
				leftKnee.TargetAngle = -5 * MathHelper.PiOver4;
				leftLowerLeg.Friction = 0f;
			}
			if (!kicking || kicking && kickLeg)
			{
				rightHip.TargetAngle = -3 * MathHelper.PiOver4;
				rightKnee.TargetAngle = -3 * MathHelper.PiOver4;
				rightLowerLeg.Friction = 0f;
			}
			AngleJoint[] checkThese = new AngleJoint[] { leftHip, leftKnee, rightHip, rightKnee };
			if (JointsAreInPosition(checkThese))
			{
				leftLowerLeg.Friction = friction;
				rightLowerLeg.Friction = friction;
			}

			// Fixes friction not working
			if (onGround && Math.Abs(torso.LinearVelocity.X) > 0.1)
				torso.ApplyForce(Vector2.UnitX * -Math.Sign(torso.LinearVelocity.X) * scale * 150f);
		}

		/// <summary>
		/// Makes figure walk the the right (place in Update method)
		/// </summary>
		public void WalkRight()
		{
			if (kicking || IsDead)
				return;

			LastFacedLeft = false;
			upright.TargetAngle = -0.1f;
			if (torso.LinearVelocity.X < (onGround ? 4 : 3) && !(Crouching && onGround))
				torso.ApplyForce(new Vector2(150, 0) * maxImpulse * (float)Math.Pow(scale, 1.5));
			AngleJoint[] checkThese = new AngleJoint[] { leftHip, rightHip };
			if (walkStage == 0)
			{
				leftHip.TargetAngle = (float)Math.PI - torso.Rotation;
				leftKnee.TargetAngle = -MathHelper.PiOver2 - torso.Rotation;
				rightHip.TargetAngle = -3 * MathHelper.PiOver4 - torso.Rotation;
				rightKnee.TargetAngle = -3 * MathHelper.PiOver4 - torso.Rotation;
				rightKnee.MaxImpulse = maxImpulse * 3 * scale * health[rightLowerLeg] * health[rightUpperLeg];
				leftLowerLeg.Friction = 0f;
				rightLowerLeg.Friction = friction;
				if (JointsAreInPosition(checkThese))
					walkStage = 1;
			}
			else if (walkStage == 1)
			{
				leftHip.TargetAngle = 3 * MathHelper.PiOver2 - torso.Rotation;
				leftKnee.TargetAngle = -MathHelper.PiOver2 - torso.Rotation;
				rightHip.TargetAngle = -5 * MathHelper.PiOver4 - torso.Rotation;
				rightKnee.TargetAngle = -(float)Math.PI - torso.Rotation;
				rightKnee.MaxImpulse = maxImpulse * scale * health[rightLowerLeg] * health[rightUpperLeg];
				if (JointsAreInPosition(checkThese))
					walkStage = 2;
			}
			else if (walkStage == 2)
			{
				leftHip.TargetAngle = 5 * MathHelper.PiOver4 - torso.Rotation;
				leftKnee.TargetAngle = -3 * MathHelper.PiOver4 - torso.Rotation;
				leftKnee.MaxImpulse = maxImpulse * 3 * scale * health[leftLowerLeg] * health[leftUpperLeg];
				rightHip.TargetAngle = -(float)Math.PI - torso.Rotation;
				rightKnee.TargetAngle = -MathHelper.PiOver2 - torso.Rotation;
				rightLowerLeg.Friction = 0f;
				leftLowerLeg.Friction = friction;
				if (JointsAreInPosition(checkThese))
					walkStage = 3;
			}
			else if (walkStage == 3)
			{
				leftHip.TargetAngle = 3 * MathHelper.PiOver4 - torso.Rotation;
				leftKnee.TargetAngle = -(float)Math.PI - torso.Rotation;
				leftKnee.MaxImpulse = maxImpulse * scale * health[leftLowerLeg] * health[leftUpperLeg];
				rightHip.TargetAngle = -MathHelper.PiOver2 - torso.Rotation;
				rightKnee.TargetAngle = -MathHelper.PiOver2 - torso.Rotation;
				if (JointsAreInPosition(checkThese))
					walkStage = 0;
			}
		}
	
		/// <summary>
		/// Makes figure walk to the left (place in Update method)
		/// </summary>
		public void WalkLeft()
		{
			if (kicking || IsDead)
				return;

			LastFacedLeft = true;
			upright.TargetAngle = 0.1f;
			if (torso.LinearVelocity.X > (onGround ? -4 : -3))
				torso.ApplyForce(new Vector2(-150, 0) * maxImpulse * (float)Math.Pow(scale, 1.5));
			AngleJoint[] checkThese = new AngleJoint[] { leftHip, rightHip };
			if (walkStage == 0)
			{
				rightHip.TargetAngle = -(float)Math.PI - torso.Rotation;
				rightKnee.TargetAngle = -3 * MathHelper.PiOver2 - torso.Rotation;
				leftHip.TargetAngle = 3 * MathHelper.PiOver4 - torso.Rotation;
				leftKnee.TargetAngle = -5 * MathHelper.PiOver4 - torso.Rotation;
				leftKnee.MaxImpulse = maxImpulse * scale * 3 * health[leftLowerLeg] * health[leftUpperLeg];
				rightLowerLeg.Friction = 0f;
				leftLowerLeg.Friction = friction;
				if (JointsAreInPosition(checkThese))
					walkStage = 1;
			}
			else if (walkStage == 1)
			{
				rightHip.TargetAngle = -3 * MathHelper.PiOver2 - torso.Rotation;
				rightKnee.TargetAngle = -3 * MathHelper.PiOver2 - torso.Rotation;
				leftHip.TargetAngle = 5 * MathHelper.PiOver4 - torso.Rotation;
				leftKnee.TargetAngle = -(float)Math.PI - torso.Rotation;
				leftKnee.MaxImpulse = maxImpulse * scale * health[leftLowerLeg] * health[leftUpperLeg];
				if (JointsAreInPosition(checkThese))
					walkStage = 2;
			}
			else if (walkStage == 2)
			{
				rightHip.TargetAngle = -5 * MathHelper.PiOver4 - torso.Rotation;
				rightKnee.TargetAngle = -5 * MathHelper.PiOver4 - torso.Rotation;
				rightKnee.MaxImpulse = maxImpulse * scale * 3 * health[rightLowerLeg] * health[rightUpperLeg];
				leftHip.TargetAngle = (float)Math.PI - torso.Rotation;
				leftKnee.TargetAngle = -3 * MathHelper.PiOver2 - torso.Rotation;
				leftLowerLeg.Friction = 0f;
				rightLowerLeg.Friction = friction;
				if (JointsAreInPosition(checkThese))
					walkStage = 3;
			}
			else if (walkStage == 3)
			{
				rightHip.TargetAngle = -3 * MathHelper.PiOver4 - torso.Rotation;
				rightKnee.TargetAngle = -(float)Math.PI - torso.Rotation;
				rightKnee.MaxImpulse = maxImpulse * scale * health[rightLowerLeg] * health[rightUpperLeg];
				leftHip.TargetAngle = MathHelper.PiOver2 - torso.Rotation;
				leftKnee.TargetAngle = -3 * MathHelper.PiOver2 - torso.Rotation;
				if (JointsAreInPosition(checkThese))
					walkStage = 0;
			}
		}

		/// <summary>
		/// Makes the figure jump
		/// </summary>
		public void Jump()
		{
			if (IsDead)
				return;

			upright.TargetAngle = 0.0f;
			if (!kicking || kicking && !kickLeg)
			{
				leftHip.TargetAngle = MathHelper.Pi;
				leftKnee.TargetAngle = -MathHelper.Pi;
			}
			if (!kicking || kicking && kickLeg)
			{
				rightHip.TargetAngle = -MathHelper.Pi;
				rightKnee.TargetAngle = -MathHelper.Pi;
			}
			if (onGround)
			{
				leftLowerLeg.Friction = friction;
				rightLowerLeg.Friction = friction;
				torso.ApplyLinearImpulse(Vector2.UnitY * (Crouching ? -25 : -15) * (float)Math.Pow(scale, 2.5)); // Change joint dependancy
			}
			Crouching = false;
		}

		/// <summary>
		/// Makes the figure crouch
		/// </summary>
		public void Crouch()
		{
			upright.TargetAngle = 0.0f;
			if (!kicking || kicking && !kickLeg)
			{
				leftLowerLeg.Friction = friction;
				leftHip.TargetAngle = MathHelper.PiOver4;
				leftKnee.TargetAngle = -7 * MathHelper.PiOver4;
			}
			if (!kicking || kicking && kickLeg)
			{
				rightLowerLeg.Friction = friction;
				rightHip.TargetAngle = -MathHelper.PiOver4;
				rightKnee.TargetAngle = -MathHelper.PiOver4;
			}
		}

		/// <summary>
		/// Punches
		/// </summary>
		/// <param name="angle">The angle at which to punch</param>
		public void Punch(float angle)
		{
			if (!IsDead)
			{
				punching = true;
				attackAngle = angle;
			}
		}

		/// <summary>
		/// Kicks
		/// </summary>
		/// <param name="angle">The angle at which to kick</param>
		public void Kick(float angle)
		{
			if (!IsDead)
			{
				kicking = true;
				kickLeg = angle > MathHelper.PiOver2 || angle < -MathHelper.PiOver2;
				attackAngle = angle;
			}
		}

		/// <summary>
		/// Readys the figure's long range attack
		/// </summary>
		/// <param name="angle">The angle to aim at</param>
		public void Aim(float angle)
		{
			if (!IsDead)
			{
				aiming = true;
				attackAngle = angle;

				if (chargeUp < MAX_CHARGE)
					chargeUp++;
			}
		}

		/// <summary>
		/// Executes a long range attack
		/// </summary>
		public void LongRangeAttack()
		{
			if (IsDead)
				return;

			aiming = false;
			if (coolDown <= 0)
			{
				attacks.Add(new LongRangeAttack(world, LeftHandPosition, (-Vector2.UnitX * (float)Math.Sin(attackAngle - MathHelper.PiOver2) - Vector2.UnitY * (float)Math.Cos(attackAngle - MathHelper.PiOver2)) * (15f + chargeUp / 15f), 0.1f + 0.2f * (chargeUp / MAX_CHARGE), collisionCat));
				chargeUp = 0;
				coolDown = COOL_PERIOD;
			}
		}

		/// <summary>
		/// Checks if all the joints in a list are close to their target angle
		/// </summary>
		/// <param name="joints">The array of joints to check</param>
		/// <returns>True if the joints are at their target angles, false if not</returns>
		private bool JointsAreInPosition(AngleJoint[] joints)
		{
			foreach (AngleJoint j in joints)
			{
				if (Math.Abs(j.BodyB.Rotation - j.BodyA.Rotation - j.TargetAngle) > 0.20)
					return false;
			}
			return true;
		}

		#endregion

		#region Updating

		/// <summary>
		/// Updates some of the stick figures' key stances
		/// </summary>
		public virtual void Update()
		{
/*			List<Body> bodies = health.Keys.ToList<Body>();
			foreach (Body b in bodies)
			{
				health[b] -= 0.002f;
			}*/

			UpdateArms();
			if (kicking)
				UpdateKicks();

			List<Attack> toRemove = new List<Attack>();
			foreach (Attack a in attacks)
			{
				a.Update();
				if (a.PhysicsBody.UserData == null)
					toRemove.Add(a);
			}
			foreach (Attack a in toRemove)
			{
				if (world.BodyList.Contains(a.PhysicsBody))
					world.RemoveBody(a.PhysicsBody);
				attacks.Remove(a);
			}
			if (coolDown > 0)
				coolDown--;

			UpdateLimbStrength();
			UpdateLimbAttachment();
			
			if (Crouching)
				Crouch();

			if (Math.Abs(torso.LinearVelocity.Y) < 0.05f)
				groundCheck++;
			else
				groundCheck = 0;
			if (groundCheck >= 5)
				onGround = true;
		}

		/// <summary>
		/// Orients arms in necessary position
		/// </summary>
		private void UpdateArms()
		{
			if (!punching && !aiming)
			{
				leftShoulder.TargetAngle = 3 * MathHelper.PiOver4;
				rightShoulder.TargetAngle = -3 * MathHelper.PiOver4;
				leftElbow.TargetAngle = MathHelper.PiOver4;
				rightElbow.TargetAngle = -MathHelper.PiOver4;
			}
			else if (aiming)
			{
				leftShoulder.TargetAngle = GetArmTargetAngle(attackAngle - MathHelper.PiOver2, true);
				rightShoulder.TargetAngle = GetArmTargetAngle(attackAngle - MathHelper.PiOver2, false);
				leftElbow.TargetAngle = 0f;
				rightElbow.TargetAngle = 0f;
			}
			else if (punching)
			{
				AngleJoint[] checkThese = new AngleJoint[] { (punchArm ? leftShoulder : rightShoulder) };
				if (punchStage == -1)
				{
					MainGame.sfx_whoosh.Play();
					punchStage = 0;
					float angle = attackAngle - MathHelper.PiOver2;
					if (punchArm)
					{
						leftShoulder.TargetAngle = GetArmTargetAngle(angle, true);
						leftElbow.TargetAngle = 0f;
						leftShoulder.MaxImpulse = 1000f * scale;
						leftElbow.MaxImpulse = 1000f * scale;
						leftUpperArm.CollidesWith = Category.Cat31;
						leftLowerArm.CollidesWith = Category.Cat31;
					}
					else
					{
						rightShoulder.TargetAngle = GetArmTargetAngle(angle, false);
						rightElbow.TargetAngle = 0f;
						rightShoulder.MaxImpulse = 1000f * scale;
						rightElbow.MaxImpulse = 1000f * scale;
						rightUpperArm.CollidesWith = Category.Cat31;
						rightLowerArm.CollidesWith = Category.Cat31;
					}
				}
				else if (punchStage == 0)
				{
					if (JointsAreInPosition(checkThese))
					{
						float angle = attackAngle - MathHelper.PiOver2;
						if (punchArm)
						{
							leftShoulder.TargetAngle = 3 * MathHelper.PiOver4;
							leftElbow.TargetAngle = MathHelper.PiOver4;
							leftShoulder.MaxImpulse = maxImpulse * scale;
							leftElbow.MaxImpulse = maxImpulse * scale;
							attacks.Add(new ForceWave(world, LeftHandPosition, new Vector2(-(float)Math.Sin(angle), -(float)Math.Cos(angle)) * 10, this.collisionCat));
						}
						else
						{
							rightShoulder.TargetAngle = -3 * MathHelper.PiOver4;
							rightElbow.TargetAngle = -MathHelper.PiOver4;
							rightShoulder.MaxImpulse = maxImpulse * scale;
							rightElbow.MaxImpulse = maxImpulse * scale;
							attacks.Add(new ForceWave(world, RightHandPosition, new Vector2(-(float)Math.Sin(angle), -(float)Math.Cos(angle)) * 10, this.collisionCat));
						}
						punchStage = 1;
					}
				}
				else if (punchStage == 1)
				{
					if (JointsAreInPosition(checkThese))
					{
						punchArm = !punchArm;
						punching = false;
						punchStage = -1;
						leftUpperArm.CollidesWith = Category.All & ~this.collisionCat;
						leftLowerArm.CollidesWith = Category.All & ~this.collisionCat;
						rightUpperArm.CollidesWith = Category.All & ~this.collisionCat;
						rightLowerArm.CollidesWith = Category.All & ~this.collisionCat;
					}
				}
			}
		}

		/// <summary>
		/// Updates the kick animation
		/// </summary>
		private void UpdateKicks()
		{
			AngleJoint[] checkThese = new AngleJoint[] { (kickLeg ? leftHip : rightHip) };
			if (kickStage == -1)
			{
				this.walkStage = 0;
				leftLowerLeg.Friction = 0f;
				rightLowerLeg.Friction = 0f;
				MainGame.sfx_whoosh.Play();
				kickStage = 0;
				float angle = attackAngle - MathHelper.PiOver2;
				if (kickLeg)
				{
					leftHip.TargetAngle = GetLegTargetAngle(angle, kickLeg);
					leftKnee.TargetAngle = -MathHelper.Pi;
					leftHip.MaxImpulse = 1000f * scale;
					leftKnee.MaxImpulse = 1000f * scale;
					leftLowerLeg.CollidesWith = Category.Cat31;
					leftUpperLeg.CollidesWith = Category.Cat31;
				}
				else
				{
					rightHip.TargetAngle = GetLegTargetAngle(angle, kickLeg);
					rightKnee.TargetAngle = -MathHelper.Pi;
					rightHip.MaxImpulse = 1000f * scale;
					rightKnee.MaxImpulse = 1000f * scale;
					rightLowerLeg.CollidesWith = Category.Cat31;
					rightUpperLeg.CollidesWith = Category.Cat31;
				}
			}
			else if (kickStage == 0)
			{
				if (JointsAreInPosition(checkThese))
				{
					float angle = attackAngle - MathHelper.PiOver2;
					if (kickLeg)
					{
						leftHip.TargetAngle = 3 * MathHelper.PiOver4;
						leftKnee.TargetAngle = -5 * MathHelper.PiOver4;
						leftHip.MaxImpulse = maxImpulse * scale;
						leftKnee.MaxImpulse = maxImpulse * scale;
						attacks.Add(new ForceWave(world, LeftFootPosition, new Vector2(-(float)Math.Sin(angle), -(float)Math.Cos(angle)) * 10, this.collisionCat));
					}
					else
					{
						rightHip.TargetAngle = -3 * MathHelper.PiOver4;
						rightKnee.TargetAngle = -3 * MathHelper.PiOver4;
						rightHip.MaxImpulse = maxImpulse * scale;
						rightKnee.MaxImpulse = maxImpulse * scale;
						attacks.Add(new ForceWave(world, RightFootPosition, new Vector2(-(float)Math.Sin(angle), -(float)Math.Cos(angle)) * 10, this.collisionCat));
					}
					kickStage = 1;
				}
			}
			else if (kickStage == 1)
			{
				if (JointsAreInPosition(checkThese))
				{
					kickLeg = !kickLeg;
					kicking = false;
					kickStage = -1;
					leftLowerLeg.CollidesWith = Category.All & ~this.collisionCat;
					leftUpperLeg.CollidesWith = Category.All & ~this.collisionCat;
					rightLowerLeg.CollidesWith = Category.All & ~this.collisionCat;
					rightUpperLeg.CollidesWith = Category.All & ~this.collisionCat;
				}
			}
		}

		/// <summary>
		/// Detaches limbs that lose all their health
		/// </summary>
		private void UpdateLimbAttachment()
		{
			// Left arm
			if (health[leftUpperArm] <= 0)
			{
				leftUpperArm.Friction = 3.0f;
				if (world.JointList.Contains(leftShoulder))
					world.RemoveJoint(leftShoulder);
				if (world.JointList.Contains(r_leftShoulder))
					world.RemoveJoint(r_leftShoulder);
				torso.OnCollision += new OnCollisionEventHandler(DamageCollisions);
			}
			if (health[leftLowerArm] <= 0)
			{
				leftLowerArm.Friction = 3.0f;
				if (world.JointList.Contains(leftElbow))
					world.RemoveJoint(leftElbow);
				if (world.JointList.Contains(r_leftElbow))
					world.RemoveJoint(r_leftElbow);
			}

			// Right arm
			if (health[rightUpperArm] <= 0)
			{
				rightUpperArm.Friction = 3.0f;
				if (world.JointList.Contains(rightShoulder))
					world.RemoveJoint(rightShoulder);
				if (world.JointList.Contains(r_rightShoulder))
					world.RemoveJoint(r_rightShoulder);
				torso.OnCollision += new OnCollisionEventHandler(DamageCollisions);
			}
			if (health[rightLowerArm] <= 0)
			{
				rightLowerArm.Friction = 3.0f;
				if (world.JointList.Contains(rightElbow))
					world.RemoveJoint(rightElbow);
				if (world.JointList.Contains(r_rightElbow))
					world.RemoveJoint(r_rightElbow);
			}

			// Left leg
			if (health[leftUpperLeg] <= 0)
			{
				leftUpperLeg.Friction = 3.0f;
				if (world.JointList.Contains(leftHip))
					world.RemoveJoint(leftHip);
				if (world.JointList.Contains(r_leftHip))
					world.RemoveJoint(r_leftHip);
				torso.OnCollision += new OnCollisionEventHandler(DamageCollisions);
			}
			if (health[leftLowerLeg] <= 0)
			{
				leftLowerLeg.Friction = 3.0f;
				if (world.JointList.Contains(leftKnee))
					world.RemoveJoint(leftKnee);
				if (world.JointList.Contains(r_leftKnee))
					world.RemoveJoint(r_leftKnee);
			}

			// Right leg
			if (health[rightUpperLeg] <= 0)
			{
				rightUpperLeg.Friction = 3.0f;
				if (world.JointList.Contains(rightHip))
					world.RemoveJoint(rightHip);
				if (world.JointList.Contains(r_rightHip))
					world.RemoveJoint(r_rightHip);
				torso.OnCollision += new OnCollisionEventHandler(DamageCollisions);
			}
			if (health[rightLowerLeg] <= 0)
			{
				rightLowerLeg.Friction = 3.0f;
				if (world.JointList.Contains(rightKnee))
					world.RemoveJoint(rightKnee);
				if (world.JointList.Contains(r_rightKnee))
					world.RemoveJoint(r_rightKnee);
			}

			// Torso
			if (health[torso] <= 0)
			{
				torso.Friction = 3.0f;
				if (world.JointList.Contains(upright))
					world.RemoveJoint(upright);
			}

			// Head
			if (health[head] <= 0)
			{
				head.Friction = 3.0f;
				if (world.JointList.Contains(neck))
					world.RemoveJoint(neck);
				if (world.JointList.Contains(r_neck))
					world.RemoveJoint(r_neck);
			}
		}

		/// <summary>
		/// Changes the MaxImpulse of each joint based on the health of its limbs
		/// TODO: More balancing
		/// </summary>
		private void UpdateLimbStrength()
		{
			List<Body> bodies = health.Keys.ToList();
			foreach (Body b in bodies)
				health[b] = Math.Max(health[b], 0f);
			upright.MaxImpulse = maxImpulse * health[torso] * health[head] * scale;
			neck.MaxImpulse = maxImpulse * health[head] * health[torso] * scale;
			leftShoulder.MaxImpulse = maxImpulse * health[torso] * health[leftUpperArm] * health[head] * scale;
			leftElbow.MaxImpulse = maxImpulse * health[leftUpperArm] * health[leftLowerArm] * health[torso] * health[head] * scale;
			rightShoulder.MaxImpulse = maxImpulse * health[torso] * health[rightUpperArm] * health[head] * scale;
			rightElbow.MaxImpulse = maxImpulse * health[rightUpperArm] * health[rightLowerArm] * health[torso] * health[head] * scale;
			leftHip.MaxImpulse = maxImpulse * health[torso] * health[leftUpperLeg] * health[head] * scale;
			leftKnee.MaxImpulse = maxImpulse * health[leftUpperLeg] * health[leftLowerLeg] * health[torso] * health[head] * scale;
			rightHip.MaxImpulse = maxImpulse * health[torso] * health[rightUpperLeg] * health[head] * scale;
			rightKnee.MaxImpulse = maxImpulse * health[rightUpperLeg] * health[rightLowerLeg] * health[torso] * health[head] * scale;
		}

		#endregion

		#region Helpers/debug

		public void ApplyForce(Vector2 v)
		{
			torso.ApplyForce(v * 10);
		}

		/// <summary>
		/// Finds the closest physical angle to a pair of numerical angles which may vary by more than 2pi
		/// </summary>
		/// <param name="physAngle">The physical angle you wish to achieve</param>
		/// <param name="numAngle1">The first numerical angle</param>
		/// <param name="numAngle2">The second numerical angle</param>
		/// <returns>A numerical angle which is physically the same as physAngle</returns>
		private float FindClosestAngle(float physAngle, float numAngle1, float numAngle2)
		{
			physAngle += numAngle1;
			while (physAngle - numAngle2 > Math.PI)
				physAngle -= MathHelper.TwoPi;
			while (physAngle - numAngle2 < -Math.PI)
				physAngle += MathHelper.TwoPi;
			return physAngle;
		}

		/// <summary>
		/// Takes an angle and converts it to an angle suitable for punching
		/// </summary>
		/// <param name="physAngle">The original angle</param>
		/// <param name="leftArm">Which arm is doing the punching</param>
		/// <returns></returns>
		private float GetArmTargetAngle(float physAngle, bool leftArm)
		{
			if (leftArm)
			{
				if (physAngle > 0f)
					return physAngle;
				else
					return physAngle + MathHelper.TwoPi;
			}
			else
			{
				if (physAngle > 0f)
					return physAngle - MathHelper.TwoPi;
				else
					return physAngle;
			}
		}

		/// <summary>
		/// Takes an angle and converts it to an angle suitable for kicking
		/// </summary>
		/// <param name="physAngle">The original angle</param>
		/// <param name="leftLeg">Which leg is kicking</param>
		/// <returns></returns>
		private float GetLegTargetAngle(float physAngle, bool leftLeg)
		{
			if (leftLeg)
			{
				if (physAngle > 0)
					return physAngle;
				else
					return physAngle + MathHelper.TwoPi;
			}
			else
				return physAngle;
		}

		#endregion

		#region Drawing

		/// <summary>
		/// Draws the stick figure
		/// </summary>
		/// <param name="sb">The SpriteBatch used to draw the stick figure</param>
		public virtual void Draw(SpriteBatch sb)
		{
			Color deathColor = Color.Black;
			Color c = Blend(color, deathColor, health[torso]);
			sb.Draw(MainGame.tex_torso, torso.Position * MainGame.METER_TO_PIXEL, null, c, torso.Rotation, new Vector2(5f, 20f), scale, LastFacedLeft ? SpriteEffects.FlipHorizontally : SpriteEffects.None, 0.0f);
			c = Blend(color, deathColor, health[leftUpperArm]);
			sb.Draw(MainGame.tex_limb, leftUpperArm.Position * MainGame.METER_TO_PIXEL, null, c, leftUpperArm.Rotation, new Vector2(5f, 12.5f), scale, LastFacedLeft ? SpriteEffects.FlipHorizontally : SpriteEffects.None, 0.0f);
			c = Blend(color, deathColor, health[rightUpperArm]);
			sb.Draw(MainGame.tex_limb, rightUpperArm.Position * MainGame.METER_TO_PIXEL, null, c, rightUpperArm.Rotation, new Vector2(5f, 12.5f), scale, LastFacedLeft ? SpriteEffects.FlipHorizontally : SpriteEffects.None, 0.0f);
			c = Blend(color, deathColor, health[leftLowerArm]);
			sb.Draw(MainGame.tex_limb, leftLowerArm.Position * MainGame.METER_TO_PIXEL, null, c, leftLowerArm.Rotation, new Vector2(5f, 12.5f), scale, LastFacedLeft ? SpriteEffects.FlipHorizontally : SpriteEffects.None, 0.0f);
			c = Blend(color, deathColor, health[rightLowerArm]);
			sb.Draw(MainGame.tex_limb, rightLowerArm.Position * MainGame.METER_TO_PIXEL, null, c, rightLowerArm.Rotation, new Vector2(5f, 12.5f), scale, LastFacedLeft ? SpriteEffects.FlipHorizontally : SpriteEffects.None, 0.0f);
			c = Blend(color, deathColor, health[leftUpperLeg]);
			sb.Draw(MainGame.tex_limb, leftUpperLeg.Position * MainGame.METER_TO_PIXEL, null, c, leftUpperLeg.Rotation, new Vector2(5f, 12.5f), scale, LastFacedLeft ? SpriteEffects.FlipHorizontally : SpriteEffects.None, 0.0f);
			c = Blend(color, deathColor, health[rightUpperLeg]);
			sb.Draw(MainGame.tex_limb, rightUpperLeg.Position * MainGame.METER_TO_PIXEL, null, c, rightUpperLeg.Rotation, new Vector2(5f, 12.5f), scale, LastFacedLeft ? SpriteEffects.FlipHorizontally : SpriteEffects.None, 0.0f);
			c = Blend(color, deathColor, health[leftLowerLeg]);
			sb.Draw(MainGame.tex_limb, leftLowerLeg.Position * MainGame.METER_TO_PIXEL, null, c, leftLowerLeg.Rotation, new Vector2(5f, 12.5f), scale, LastFacedLeft ? SpriteEffects.FlipHorizontally : SpriteEffects.None, 0.0f);
			c = Blend(color, deathColor, health[rightLowerLeg]);
			sb.Draw(MainGame.tex_limb, rightLowerLeg.Position * MainGame.METER_TO_PIXEL, null, c, rightLowerLeg.Rotation, new Vector2(5f, 12.5f), scale, LastFacedLeft ? SpriteEffects.FlipHorizontally : SpriteEffects.None, 0.0f);
			c = Blend(color, deathColor, health[head]);
			sb.Draw(MainGame.tex_head, head.Position * MainGame.METER_TO_PIXEL, null, c, head.Rotation, new Vector2(12.5f, 12.5f), scale, LastFacedLeft ? SpriteEffects.FlipHorizontally : SpriteEffects.None, 0.0f);

			foreach (Attack a in attacks)
				a.Draw(sb, this.color);

			// Debug
//			sb.DrawString(MainGame.fnt_basicFont, attackAngle.ToString(), Vector2.One * 64, Color.White); 
//			sb.DrawString(MainGame.fnt_basicFont, "L", LeftFootPosition * MainGame.METER_TO_PIXEL, Color.Blue);
//			sb.DrawString(MainGame.fnt_basicFont, "R", RightFootPosition * MainGame.METER_TO_PIXEL, Color.Lime);
//			sb.DrawString(MainGame.fnt_basicFont, leftLowerLeg.Friction.ToString(), Vector2.UnitY * 32, Color.White);
//			sb.DrawString(MainGame.fnt_basicFont, rightLowerLeg.Friction.ToString(), Vector2.UnitY * 64, Color.White);
		}

		/// <summary>Blends the specified colors together.</summary>
		/// <param name="color">Color to blend onto the background color.</param>
		/// <param name="backColor">Color to blend the other color onto.</param>
		/// <param name="amount">How much of <paramref name="color"/> to keep,
		/// “on top of” <paramref name="backColor"/>.</param>
		/// <returns>The blended colors.</returns>
		private Color Blend(Color c1, Color c2, float amount)
		{
			byte r = (byte)((c1.R * amount) + c2.R * (1 - amount));
			byte g = (byte)((c1.G * amount) + c2.G * (1 - amount));
			byte b = (byte)((c1.B * amount) + c2.B * (1 - amount));
			return Color.FromNonPremultiplied(r, g, b, c1.A);
		}

		#endregion
	}
}
