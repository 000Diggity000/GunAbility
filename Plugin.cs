using AbilityApi;
using BepInEx;
using BoplFixedMath;
using HarmonyLib;
using Mono.Unix.Native;
using Steamworks.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.SocialPlatforms;
using static System.Net.Mime.MediaTypeNames;

namespace GunAbility
{
    [BepInPlugin("org.000diggity000.LongAbility", "LongAbility", "1.0.1")]
    public class Plugin : BaseUnityPlugin
    {
        public static System.IO.Stream GetResourceStream(string namespaceName, string path) => Assembly.GetExecutingAssembly().GetManifestResourceStream($"{namespaceName}.{path}");
        GameObject BowObject;
        public static BoplBody Arrow;
        private void Awake()
        {
            Harmony harmony = new Harmony("org.000diggity000.LongAbility");
            
            MethodBase original = AccessTools.Method(typeof(Arrow), "OnCollide");
            harmony.Patch(original, new HarmonyMethod(AccessTools.Method(typeof(Patches), "OnCollide")));
            harmony.PatchAll();
            var directoryToModFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var LongAbilityPrefab = ConstructAbility<GunAbility>("Gun");
            LongAbilityPrefab.gameObject.AddComponent<PlayerPhysics>();
            Texture2D LongAbilityTex = new Texture2D(1, 1);
            LongAbilityTex.LoadImage(ReadFully(GetResourceStream("GunAbility", "AbilityIcon.png")));
            var iconSprite = Sprite.Create(LongAbilityTex, new Rect(0f, 0f, LongAbilityTex.width, LongAbilityTex.height), new Vector2(0.5f, 0.5f));
            NamedSprite longAbility = new NamedSprite("Gun", iconSprite, LongAbilityPrefab.gameObject, true);
            Api.RegisterNamedSprites(longAbility, true);

            //David code
            GameObject[] array = Resources.FindObjectsOfTypeAll(typeof(GameObject)) as GameObject[];
            GameObject[] array2 = array;

            foreach (GameObject val2 in array2)
            {
                if (((UnityEngine.Object)val2).name == "Bow")
                {
                    BowObject = val2;
                    break;
                }
            }
            Component component = BowObject.GetComponent(typeof(BowTransform));
            BowTransform obj = (BowTransform)(object)((component is BowTransform) ? component : null);
            Arrow = (BoplBody)AccessTools.Field(typeof(BowTransform), "Arrow").GetValue(obj);
        }
        public static byte[] ReadFully(Stream input)
        {
            byte[] buffer = new byte[16 * 1024];
            using (MemoryStream ms = new MemoryStream())
            {
                int read;
                while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
                {
                    ms.Write(buffer, 0, read);
                }
                return ms.ToArray();
            }
        }
        public static T ConstructAbility<T>(string name) where T : MonoUpdatable
        {
            GameObject parent = new GameObject(name);
            GameObject.DontDestroyOnLoad(parent);

            Ability ability = parent.AddComponent<Ability>();

            parent.AddComponent<FixTransform>();
            parent.AddComponent<SpriteRenderer>();
            Texture2D LongAbilityTex = new Texture2D(2, 2);
            LongAbilityTex.LoadImage(ReadFully(GetResourceStream("GunAbility", "gun.png")));
            var iconSprite = Sprite.Create(LongAbilityTex, new Rect(0f, 0f, LongAbilityTex.width, LongAbilityTex.height), new Vector2(0.5f, 0.5f));
            parent.GetComponent<SpriteRenderer>().sprite = iconSprite;
            parent.GetComponent<SpriteRenderer>().enabled = false;
            parent.AddComponent<PlayerBody>();
            parent.AddComponent<DPhysicsBox>();
            parent.GetComponent<DPhysicsBox>().Scale = (Fix)1.5f;
            parent.AddComponent<PlayerCollision>();
            MonoUpdatable updatable = parent.AddComponent<T>();

            if (updatable == null)
            {
                GameObject.Destroy(parent);
                throw new MissingReferenceException("Invalid type was fed to ConstructAbility");
            }

            return (T)updatable;
        }
    }
    public class GunAbility : MonoUpdatable, IAbilityComponent
    {
        public Ability ab;
        Player player;
        FixTransform playerTransform;
        PlayerBody body;
        PlayerPhysics playerPhysics;
        SpriteRenderer spriteRenderer;
        bool hasFired = true;
        bool releasedButton;
        Vec2 inputVector;
        Vec2 actualInputVector;
        Fix FirePointOffsetX = (Fix)3f;
        Fix FirePointOffSetY = -(Fix)3f;
        RingBuffer<BoplBody> Bullets;
        BoplBody Bullet;
        public void Awake()
        {
            Updater.RegisterUpdatable(this);
            ab = GetComponent<Ability>();
            ab.Cooldown = (Fix)4;
            //playerTransform = base.GetComponent<FixTransform>();
            //body = base.GetComponent<SpriteRenderer>();
            
            spriteRenderer = GetComponent<SpriteRenderer>();
            playerPhysics = GetComponent<PlayerPhysics>();
            body = GetComponent<PlayerBody>();
            player = PlayerHandler.Get().GetPlayer(ab.GetPlayerInfo().playerId);
            Texture2D LongAbilityTex = new Texture2D(2, 2);
            LongAbilityTex.LoadImage(Plugin.ReadFully(Plugin.GetResourceStream("GunAbility", "gun.png")));
            var iconSprite = Sprite.Create(LongAbilityTex, new Rect(0f, 0f, LongAbilityTex.width, LongAbilityTex.height), new Vector2(0.5f, 0.5f));
            Bullets = new RingBuffer<BoplBody>(10);
        }
        public void Update()
        {

        }


        public override void Init()
        {

        }
        private void OldUpdate(Fix simDeltaTime)
        {
            if (player == null)
            {
                return;
            }
            else
            {
                Vec2 v = player.AimVector();
                if (Vec2.Magnitude(v) > (Fix)0.4 && !this.releasedButton && !hasFired)
                {
                    Vec2 newV = new Vec2((Fix)1 / v.x, -(Fix)1 / v.y);
                    this.inputVector = Vec2.Normalized(newV);
                    //float rotation = (float)(Math.Atan2((float)inputVector.y, (float)inputVector.x) / (2 * Math.PI));
                    //Debug.Log(rotation);
                    //if (rotation > 0 && rotation < 0.26)
                    //{
                    //spriteRenderer.flipX = true;
                    //spriteRenderer.flipY = true;

                    //}
                    //if (rotation > 0.5 && rotation < 0.76)
                    //{
                    //spriteRenderer.flipY = true;
                    //spriteRenderer.flipX = false;
                    //}
                }
                Vec2 V = player.AimVector();
                if (Vec2.Magnitude(v) > (Fix)0.4 && !this.releasedButton && !hasFired)
                {
                    actualInputVector = Vec2.Normalized(V);
                    //float rotation = (float)(Math.Atan2((float)inputVector.y, (float)inputVector.x) / (2 * Math.PI));
                    //Debug.Log(rotation);
                    //if (rotation > 0 && rotation < 0.26)
                    //{
                    //spriteRenderer.flipX = true;
                    //spriteRenderer.flipY = true;

                    //}
                    //if (rotation > 0.5 && rotation < 0.76)
                    //{
                    //spriteRenderer.flipY = true;
                    //spriteRenderer.flipX = false;
                    //}
                }
            }
            if (!player.AbilityButtonIsDown(ab.GetPlayerInfo().AbilityButtonUsedIndex012) && !hasFired)
            {
                Fire();
                releasedButton = true;

            }
        }
        public override void UpdateSim(Fix SimDeltaTime)
        {
            OldUpdate(SimDeltaTime);
            if (inputVector != null && !hasFired)
            {
                float rotationRotation = (float)(Math.Atan2((float)inputVector.y, (float)inputVector.x) / (2 * Math.PI));
                body.rotation = (Fix)rotationRotation;
                float rotation = rotationRotation;
                rotation *= 360;
                //Debug.Log(rotation);
                if (rotation > 0 && rotation < 90)
                {
                    //.Log("Left Up");
                    spriteRenderer.flipX = false;
                    spriteRenderer.flipY = false;
                }
                if (rotation > 90 && rotation < 180)
                {
                    //Debug.Log("Down Left");
                    spriteRenderer.flipX = true;
                    spriteRenderer.flipY = false;
                }
                if (rotation > -180 && rotation < -90)
                {
                    //Debug.Log("Down Right");
                    spriteRenderer.flipX = false;
                    spriteRenderer.flipY = true;
                }
                if (rotation > -90 && rotation < 0)
                {
                    //Debug.Log("Right Up");
                    spriteRenderer.flipX = true;
                    spriteRenderer.flipY = true;
                }

            }
            if (playerPhysics == null)
            {
                return;
            }
            if (SceneBounds.WaterHeight > body.position.y - (Fix)0.2f)
            {
                AbilityExitInfo info = default(AbilityExitInfo);
                info.position = body.position;
                info.selfImposedVelocity = body.selfImposedVelocity;
                ExitAbility(info);
            }
            if (playerPhysics.IsGrounded() && (Vec2.SqrMagnitude(this.body.selfImposedVelocity) > (Fix)1E-06f || playerPhysics.getAttachedGround() == null || !this.playerPhysics.getAttachedGround().isActiveAndEnabled))
            {
                this.playerPhysics.gravity_modifier = (Fix)0.0f;
                this.playerPhysics.UnGround(false, true);
            }
            if (!this.playerPhysics.IsGrounded())
            {
                this.playerPhysics.AddGravityFactor();
                if (this.playerPhysics.VelocityBasedRaycasts(true, SimDeltaTime) && this.hasFired)
                {
                    AbilityExitInfo info = default(AbilityExitInfo);
                    this.body.rotation = Fix.Zero;
                    info.justlanded = true;
                    info.groundedSpeed = playerPhysics.groundedSpeed;
                    info.isGrounded = playerPhysics.IsGrounded();
                    info.isFacingRight = (this.inputVector.x >= 0L);
                    info.position = this.body.position;
                    info.selfImposedVelocity = this.body.selfImposedVelocity;
                    info.externalVelocity = this.body.externalVelocity;
                    info.currentlyAttachedGround = this.playerPhysics.getAttachedGround();
                    info.lastSprite = spriteRenderer.sprite;
                    info.groundedLocalPosition = playerPhysics.LocalPlatformPos;
                    info.radius = playerPhysics.radius;
                    ab.ExitAbility(info);
                }
            }
            if (!this.hasFired)
            {
                this.body.up = this.inputVector;
                if (Vec2.Magnitude(this.body.selfImposedVelocity) > (Fix)9f)
                {
                    this.body.selfImposedVelocity = Vec2.Normalized(this.body.selfImposedVelocity) * (Fix)9f;
                }
            }
            else
            {
                Vec2 selfImposedVelocity = this.body.selfImposedVelocity;
                Fix inputHorz = this.player.HorizontalAxis();
                playerPhysics.AirealMovement(inputHorz, SimDeltaTime);
                Vec2 selfImposedVelocity2 = this.body.selfImposedVelocity;
                this.body.selfImposedVelocity = Vec2.Lerp(selfImposedVelocity, selfImposedVelocity2, (Fix)0.5f);
            }
            if (Vec2.Magnitude(this.body.selfImposedVelocity) > playerPhysics.Speed)
            {
                this.body.selfImposedVelocity = Vec2.Normalized(this.body.selfImposedVelocity) * playerPhysics.Speed;
            }
            if (playerPhysics.IsGrounded())
            {
                playerPhysics.gravity_modifier = Fix.Zero;
                playerPhysics.UnGround(false, false);
                playerPhysics.DropPlatformTest();
            }


        }

        public void OnEnterAbility()
        {
            //for sprite
            spriteRenderer.material = ab.GetPlayerInfo().playerMaterial;
            spriteRenderer.enabled = true;
            spriteRenderer.flipX = false;
            spriteRenderer.flipY = false;
            //set right position
            body.position = ab.GetPlayerInfo().position;
            body.rotation = (Fix)0L;
            //make gravity work
            playerPhysics.SyncPhysicsTo(ab.GetPlayerInfo());
            //set the player
            player = PlayerHandler.Get().GetPlayer(ab.GetPlayerInfo().playerId);
            //Set bools. One would have been fine, but maybe useful.

            releasedButton = false;
            hasFired = false;
            inputVector = ab.GetPlayerInfo().upVector;
            body.rotation = Fix.Zero;
            playerPhysics.gravity_modifier = Fix.One;
            playerPhysics.UnGround(false, true);
        }
        public void Fire()
        {
            AudioManager.Get().Play("explosion");
            //broadcast info so you can exit the ability normally
            AbilityExitInfo info = default(AbilityExitInfo);
            info.position = body.position;
            info.selfImposedVelocity = body.selfImposedVelocity;

            float rotationRotation = (float)(Math.Atan2((float)inputVector.y, (float)inputVector.x) / (2 * Math.PI));
            body.rotation = (Fix)rotationRotation;
            float rotation = rotationRotation;
            rotation *= 360;
            Vec2 pos = CurrentFirePoint();
            
            //pos = new Vec2(body.position.x + FirePointOffsetX, body.position.y + FirePointOffSetY);
            //GameObject test = new GameObject();
            //test.AddComponent<BoplBody>();
            //BoplBody boplBody = FixTransform.InstantiateFixed<BoplBody>(new BoplBody(), new Vec2((Fix)100, (Fix)100), this.body.rotation);
            //GameObject bullet = GameObject.Instantiate(new GameObject(), (Vector2)pos, body.gameObject.transform.rotation);
            BoplBody boplBody = FixTransform.InstantiateFixed<BoplBody>(Plugin.Arrow, CurrentFirePoint(), this.body.rotation);
            boplBody.gameObject.name = "bullet-diggity";
            boplBody.GetComponent<Arrow>().StickTo = new LayerMask();
            Bullets.Add(boplBody);
            //Destroy(test);
            Texture2D LongAbilityTex = new Texture2D(2, 2);
            LongAbilityTex.LoadImage(Plugin.ReadFully(Plugin.GetResourceStream("GunAbility", "bullet.png")));
            var iconSprite = Sprite.Create(LongAbilityTex, new Rect(0f, 0f, LongAbilityTex.width, LongAbilityTex.height), new Vector2(0.5f, 0.5f));
            boplBody.GetComponent<SpriteRenderer>().sprite = iconSprite;
            boplBody.GetComponent<SpriteRenderer>().material = ab.GetPlayerInfo().playerMaterial;
            boplBody.Scale = (Fix)0.3f * body.fixtrans.Scale;
            //boplBody.Scale = this.body.fixtrans.Scale;
            Fix fix = Fix.One + (this.body.fixtrans.Scale - Fix.One) / (Fix)2L;
            boplBody.GetComponent<IPlayerIdHolder>().SetPlayerId(ab.GetPlayerInfo().playerId);
            inputVector = Vec2.Normalized(inputVector);
            boplBody.GetComponent<SpriteRenderer>().material = ab.GetPlayerInfo().playerMaterial;
            boplBody.StartVelocity = actualInputVector * (Fix)55 * Fix.One * fix;
            boplBody.GetComponent<Projectile>().DelayedEnableHurtOwner((Fix)0.3f);
            //boplBody.rotation = this.body.rotation;
            //boplBody.up = this.body.up;
            boplBody.gravityScale = (Fix)0;
            this.hasFired = true;
            //call the exit ability function
            boplBody.rotation = this.body.rotation;
            boplBody.up = this.body.up;
            boplBody.right = this.body.right;
            ab.ExitAbility(info);
        }

        public void ExitAbility(AbilityExitInfo info)
        {
            enabled = false;
            playerPhysics.gravity_modifier = Fix.One;
            spriteRenderer.flipX = false;
            spriteRenderer.flipY = false;
            ab.ExitAbility(info);
        }
        public Vec2 CurrentFirePoint()
        {
            return this.body.position + this.FirePointOffSetY * Vec2.Normalized(player.AimVector()) * this.body.fixtrans.Scale + this.FirePointOffsetX * Vec2.Normalized(player.AimVector()) * this.body.fixtrans.Scale * (Fix)1.5f;
        }

        public void OnScaleChanged(Fix scaleMultiplier)
        {
            throw new NotImplementedException();
        }

    }
    
    public class Patches
    {
        public static bool OnCollide(Arrow __instance)
        {
            if(__instance.gameObject.name == "bullet-diggity")
            {
                Updater.DestroyFix(__instance.gameObject);
                return false;
            }
            return true;
        }
    }
}