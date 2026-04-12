using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Rigidbody2D))] // Rigidbody2Dを必須にする
[RequireComponent(typeof(AudioSource))] // 音を鳴らすためにAudioSourceを必須にする
public class CaterpillarEnemy : MonoBehaviour
{
    // 既存の移動設定
    [Header("通常移動の設定")]
    public float maxScaleX = 1.5f;
    public float stretchTime = 0.5f;
    public float shrinkTime = 0.4f;
    public float moveDistance = 1.0f;
    public float pauseTime = 0.2f;
    public bool isFacingRight = false;

    [Header("パトロール設定")]
    public float patrolRange = 5.0f; // 初期位置から左右に移動する最大距離
    private float startX; // 初期位置のX座標を記憶

    // 大ジャンプ攻撃の設定
    [Header("ジャンプ攻撃の設定")]
    public float jumpAttackInterval = 5.0f; // 何秒ごとにジャンプするか
    public float jumpForceX = 5.0f;          // 前方へのジャンプ力
    public float jumpForceY = 10.0f;         // 上方へのジャンプ力
    
    [Header("ジャンプ予備動作の設定 (芋虫の潰れ)")]
    public float jumpPrepareTime = 0.8f;    // 力を溜める時間
    public float jumpPrepareScaleY = 0.5f; // どれくらい縦に潰れるか
    public float jumpPrepareScaleX = 1.8f; // どれくらい横に伸びるか

    [Header("衝撃波の設定")]
    public GameObject shockwavePrefab;        // 衝撃波のプレハブ
    public float shockwaveSpeed = 8.0f;        // 衝撃波の速さ
    public Vector2 shockwaveOffset = new Vector2(0f, -0.5f); // 生成位置の微調整
    
    // 頭と尻尾の位置を調整するための変数
    public float bodyLength = 1.5f; // 芋虫の頭から尻尾までの長さ

    // 悪臭噴射攻撃の設定
    [Header("悪臭噴射攻撃の設定")]
    public GameObject gasPrefab;         // 悪臭ガスのプレハブ
    public float gasAttackCooldown = 3.0f; // 次に噴射するまでの時間
    public float gasDuration = 1.0f;       // 噴射している時間
    private float gasTimer = 0f;           // クールダウン計測用

    // 新規追加: 効果音の設定
    [Header("効果音の設定")]
    public AudioClip jumpSound;       // ジャンプした瞬間の音
    public AudioClip landingSound;    // 着地して衝撃波を出した時の音
    public AudioClip gasSound;        // 悪臭ガスを噴き出した時の音
    private AudioSource audioSource;  // 音を鳴らすためのスピーカー

    // HPとステータス異常の設定
    [Header("ステータス設定")]
    public float maxHP = 5000f;            // 敵の最大HP
    public float currentHP;               // 敵の現在のHP
    private bool isPoisoned = false;      // 毒状態かどうか
    private SpriteRenderer spriteRenderer; // 毒の色変化用

    // ダメージポップアップの設定
    [Header("UI設定")]
    public GameObject damagePopupPrefab;  // お手持ちのダメージ数字プレハブを登録

    public Transform hpBarTransform;      // 敵の体力ゲージ（伸縮させるバーのTransform）
    private Vector3 originalHpBarScale;   // バーの元の大きさ（最大HPの時のScale）を記憶する変数

    // やられ演出の設定
    [Header("やられ演出の設定")]
    public float fadeOutTime = 1.0f; // 何秒かけて透明になるか
    private bool isDead = false;     // すでにやられているか

    // 内部変数
    private Vector3 originalScale;
    private Rigidbody2D rb;
    private bool isGrounded = true;           // 地面に着いているか
    private Coroutine moveCoroutine;          // 通常移動のコルーチン管理用
    private float jumpTimer;

    //  プレイヤーの位置を知るための変数
    private Transform playerTransform;

    // 状態管理
    private enum EnemyState { Moving, Jumping, GasAttack } 
    private EnemyState currentState = EnemyState.Moving;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        originalScale = transform.localScale;
        
        // HPとSpriteRendererの初期化
        currentHP = maxHP;
        spriteRenderer = GetComponent<SpriteRenderer>();

        // ゲーム開始時に体力ゲージの元のScale（大きさ）を記憶しておく
        if (hpBarTransform != null)
        {
            originalHpBarScale = hpBarTransform.localScale;
        }

        // 重力などを無視しつつ当たり判定を残す
        rb.bodyType = RigidbodyType2D.Kinematic; 

        startX = transform.position.x; // ここで初期位置を記憶する

        // ゲーム開始時に Inspector の Is Facing Right の設定に合わせて向きをセットする
        if (isFacingRight)
        {
            // 今回は画像が左向きなので180度が右向きになる
            transform.localRotation = Quaternion.Euler(0, 180f, 0);
        }
        else
        {
            transform.localRotation = Quaternion.Euler(0, 0, 0);
        }

        // プレイヤーを自動的に探して記憶する
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerTransform = player.transform;
        }

        // AudioSourceを取得して初期設定をする
        audioSource = GetComponent<AudioSource>();
        if (audioSource != null)
        {
            audioSource.playOnAwake = false;
            // タイトル画面で設定・保存されたSE音量を読み込んで適用し、unityroom向けに初期音量を0.5fに設定した
            audioSource.volume = PlayerPrefs.GetFloat("SE_Volume", 0.5f);
        }

        // 通常移動を開始
        moveCoroutine = StartCoroutine(MoveRoutine());
        jumpTimer = jumpAttackInterval;
    }

    void Update()
    {
        // やられていたらこれ以上何もしない
        if (isDead) return;

        // クールダウンタイマーを減らす
        if (gasTimer > 0f)
        {
            gasTimer -= Time.deltaTime;
        }

        // 通常移動中のみジャンプのタイマーを進める
        if (currentState == EnemyState.Moving)
        {
            jumpTimer -= Time.deltaTime;
            if (jumpTimer <= 0f)
            {
                // ジャンプ攻撃ルーチンを開始
                StartCoroutine(JumpAttackRoutine());
                jumpTimer = jumpAttackInterval; // タイマーリセット
            }
        }
    }

    // ダメージを受ける処理 (毒ダメージかどうかの判定を追加しました)
    public void TakeDamage(float damage, bool isPoison = false)
    {
        // すでにやられている時はダメージを受け付けない
        if (isDead) return;

        currentHP -= damage;
        Debug.Log("敵にダメージ！ 残りHP: " + currentHP);

        // ダメージを受けたら、残りHPの割合を計算してバーのScale(X)を減らす
        if (hpBarTransform != null)
        {
            float hpRatio = currentHP / maxHP;
            if (hpRatio < 0f) hpRatio = 0f; // マイナスになってバーが逆向きに伸びないようにする安全対策
            
            // YとZの大きさはそのままに、Xの大きさだけを割合に合わせて縮める
            hpBarTransform.localScale = new Vector3(originalHpBarScale.x * hpRatio, originalHpBarScale.y, originalHpBarScale.z);
        }

        // ダメージポップアップの生成
        if (DamagePopUpManager.Instance != null)
        {
            Collider2D col = GetComponent<Collider2D>();
            float boundsY = col != null ? col.bounds.size.y : 2.0f; // エラー対策
            
            // 剣攻撃と同じ計算式に変更（敵のColliderサイズを取得して頭上に表示）
            Vector3 popUpPos = transform.position + new Vector3(0, boundsY / 2f + 0.5f, 0);
            bool isCritical = (damage >= DamagePopUpManager.Instance.critThreshold);
            
            // 「毒かどうか(isPoison)」をマネージャーに渡すようにする
            DamagePopUpManager.Instance.ShowDamage(damage, popUpPos, isCritical, isPoison);
        }

        // HPが0以下になったら消滅する
        if (currentHP <= 0)
        {
            // やられた状態にする
            isDead = true;

            // 倒れたら体力ゲージを画面から消す
            if (hpBarTransform != null)
            {
                // 背景の枠などがある場合、親オブジェクトごと消すのが綺麗です
                if (hpBarTransform.parent != null)
                {
                    hpBarTransform.parent.gameObject.SetActive(false);
                }
                else
                {
                    hpBarTransform.gameObject.SetActive(false);
                }
            }

            // ボスが倒れたらGameTimerを探してクリア処理を呼び出す
            GameTimer timer = Object.FindFirstObjectByType<GameTimer>();
            if (timer != null)
            {
                timer.GameClear();
            }

            // すぐに消えずに、フェードアウトするコルーチンを呼び出す
            StartCoroutine(DieRoutine());
        }
    }

    // やられた時にゆっくり透明になってから消える処理
    IEnumerator DieRoutine()
    {
        // 移動などの他の処理をすべて止める
        if (moveCoroutine != null) StopCoroutine(moveCoroutine);
        
        // 当たり判定を消して、プレイヤーが通り抜けられるようにする
        Collider2D col = GetComponent<Collider2D>();
        if (col != null)
        {
            col.enabled = false;
        }

        // 物理的な動きをピタッと止める
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.linearVelocity = Vector2.zero;

        // 徐々に透明にする
        if (spriteRenderer != null)
        {
            Color startColor = spriteRenderer.color;
            float timer = 0f;

            while (timer < fadeOutTime)
            {
                // スローの影響を受けない「現実の時間（unscaledDeltaTime）」を使って一定の速度で消す
                timer += Time.unscaledDeltaTime;
                float t = timer / fadeOutTime;
                float alpha = Mathf.Lerp(startColor.a, 0f, t);
                spriteRenderer.color = new Color(startColor.r, startColor.g, startColor.b, alpha);
                
                yield return null;
            }
        }

        // 完全に透明になったらオブジェクトを削除する
        Destroy(gameObject);
    }

    // 毒状態になる処理
    public void ApplyPoison(float duration, float damagePerTick, float tickInterval)
    {
        // すでに毒状態なら重複しないようにする
        if (!isPoisoned)
        {
            StartCoroutine(PoisonRoutine(duration, damagePerTick, tickInterval));
        }
    }

    // 毒の継続ダメージと色の変化を管理するコルーチン
    IEnumerator PoisonRoutine(float duration, float damagePerTick, float tickInterval)
    {
        isPoisoned = true;
        float elapsed = 0f;

        // 色を紫色にする
        Color normalColor = Color.white;
        if (spriteRenderer != null)
        {
            normalColor = spriteRenderer.color;
            spriteRenderer.color = new Color(0.9f, 0.7f, 1f); // 薄い紫色
        }

        // 指定された時間（duration）が終わるまで繰り返す
        while (elapsed < duration)
        {
            // 待機時間（tickInterval）だけ待つ
            yield return new WaitForSeconds(tickInterval);
            elapsed += tickInterval;
            
            // 継続ダメージを与える (毒ダメージなので true を渡す)
            TakeDamage(damagePerTick, true);
        }

        // 毒が終わったら元の色に戻す
        if (spriteRenderer != null)
        {
            spriteRenderer.color = normalColor;
        }
        isPoisoned = false;
    }

    // 既存の移動ルーチン (少し修正)
    IEnumerator MoveRoutine()
    {
        while (true)
        {
            // 動く前にプレイヤーがいる方向を向く
            if (playerTransform != null)
            {
                if (playerTransform.position.x > transform.position.x && !isFacingRight)
                {
                    isFacingRight = true;
                    // 画像が左向きなので180度で右を向く
                    transform.localRotation = Quaternion.Euler(0, 180f, 0);
                }
                else if (playerTransform.position.x < transform.position.x && isFacingRight)
                {
                    isFacingRight = false;
                    // 画像が左向きなので0度で左を向く
                    transform.localRotation = Quaternion.Euler(0, 0, 0);
                }
            }

            // 伸びるフェーズ (お尻を残して前方にスケールを伸ばす)
            float timer = 0f;
            while (timer < stretchTime)
            {
                timer += Time.deltaTime;
                // Lerpを使って徐々にスケールを大きくする
                float currentScaleX = Mathf.Lerp(originalScale.x, originalScale.x * maxScaleX, timer / stretchTime);
                transform.localScale = new Vector3(currentScaleX, originalScale.y, originalScale.z);
                yield return null;
            }

            // 見えない光線（レイキャスト）で確実に前方の壁をチェックする
            float direction = isFacingRight ? 1f : -1f;
            Vector2 rayDir = isFacingRight ? Vector2.right : Vector2.left;
            
            // 頭の位置から少し前方に光線を飛ばす
            RaycastHit2D hit = Physics2D.Raycast(transform.position, rayDir, bodyLength + 0.5f);
            
            // 光線が Wall タグにぶつかったら反転する
            if (hit.collider != null && hit.collider.CompareTag("Wall"))
            {
                isFacingRight = !isFacingRight;
                direction = isFacingRight ? 1f : -1f; // 反転した新しい方向に入れ直す
                
                if (isFacingRight)
                {
                    transform.localRotation = Quaternion.Euler(0, 180f, 0);
                }
                else
                {
                    transform.localRotation = Quaternion.Euler(0, 0, 0);
                }
            }

            // 縮みながら進むフェーズ (スケールを戻しつつ、実際のPositionを前に進める)
            timer = 0f;
            Vector3 startPos = transform.position;
            Vector3 targetPos = startPos + new Vector3(moveDistance * direction, 0f, 0f);

            while (timer < shrinkTime)
            {
                timer += Time.deltaTime;
                float t = timer / shrinkTime;
                
                // スケールを元のサイズに戻す
                float currentScaleX = Mathf.Lerp(originalScale.x * maxScaleX, originalScale.x, t);
                transform.localScale = new Vector3(currentScaleX, originalScale.y, originalScale.z);
                
                // transform.positionを直接動かし、同時に位置をターゲット地点まで進める
                transform.position = Vector3.Lerp(startPos, targetPos, t);
                yield return null;
            }

            // ズレを防止するために、最後に数値をカッチリ合わせる
            transform.localScale = originalScale;
            transform.position = targetPos;

            // 座標による方向転換チェック(パトロール範囲を超えたら強制的に向きを戻す)
            if (isFacingRight && transform.position.x >= startX + patrolRange)
            {
                isFacingRight = false;
                // Y軸を0度にして画像を左向きにする
                transform.localRotation = Quaternion.Euler(0, 0, 0); 
            }
            else if (!isFacingRight && transform.position.x <= startX - patrolRange)
            {
                isFacingRight = true;
                // 180度にして画像を右向きにする
                transform.localRotation = Quaternion.Euler(0, 180f, 0); 
            }

            // 少し待機
            yield return new WaitForSeconds(pauseTime);
        }
    }

    // 大ジャンプ攻撃ルーチン
    IEnumerator JumpAttackRoutine()
    {
        currentState = EnemyState.Jumping;

        // 通常移動を停止し、物理演算をONにする
        if (moveCoroutine != null) StopCoroutine(moveCoroutine);
        
        // 予備動作：Yを縮めてXを伸ばす（グググッというタメ）
        float timer = 0f;
        Vector3 prepareScale = new Vector3(originalScale.x * jumpPrepareScaleX, originalScale.y * jumpPrepareScaleY, originalScale.z);
        
        while (timer < jumpPrepareTime)
        {
            timer += Time.deltaTime;
            transform.localScale = Vector3.Lerp(originalScale, prepareScale, timer / jumpPrepareTime);
            yield return null;
        }

        // 地面へのめり込みを強制解除するため、ほんの少し上にずらす
        transform.position += new Vector3(0, 0.05f, 0);

        // 物理演算を有効にしてジャンプする
        rb.bodyType = RigidbodyType2D.Dynamic; 
        isGrounded = false;
        
        float direction = isFacingRight ? 1f : -1f;

        // ジャンプした瞬間にジャンプ音を鳴らす
        if (jumpSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(jumpSound);
        }

        // 瞬間的な力を加える (Impulse)
        rb.AddForce(new Vector2(jumpForceX * direction, jumpForceY), ForceMode2D.Impulse);

        // ジャンプした瞬間にスケールを元に戻す
        transform.localScale = originalScale;

        // 確実に空中に飛び立つまで待機し、念のため接地判定を上書きリセット
        yield return new WaitForSeconds(0.15f);
        isGrounded = false;

        // 着地するまで待つ (OnCollisionStay2DでisGroundedがtrueになるのを待つ)
        while (!isGrounded)
        {
            // 速度が完全に0になったら着地とみなす
            if (rb.linearVelocity.y == 0f) isGrounded = true;
            yield return null;
        }

        // 着地して衝撃波を生成し、その瞬間にドスンという音を鳴らす
        if (landingSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(landingSound);
        }
        SpawnShockwaves();

        // 着地の反動（少し動けない時間）
        yield return new WaitForSeconds(0.5f);

        // 通常移動に復帰
        //当たり判定以外の物理演算を無効化）し、念のため余計な速度をリセットする
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.linearVelocity = Vector2.zero;
        
        transform.localScale = originalScale; // スケール念押しリセット
        moveCoroutine = StartCoroutine(MoveRoutine());
        currentState = EnemyState.Moving;
    }

    // 頭と尻尾の計算をシンプルかつ正確に
    void SpawnShockwaves()
    {
        if (shockwavePrefab == null) return;

        // 生成位置の基本 (敵の足元)
        // 尻尾（ピボット位置）
        Vector3 tailPos = transform.position + (Vector3)shockwaveOffset;
        
        // 頭の位置（向いている方向へ bodyLength 分進めた位置）
        float direction = isFacingRight ? 1f : -1f;
        Vector3 headPos = tailPos + new Vector3(bodyLength * direction, 0f, 0f);

        // 頭側から進行方向へ衝撃波
        GameObject waveFront = Instantiate(shockwavePrefab, headPos, Quaternion.identity);
        // プレハブの元の大きさを維持したまま反転させるようにする
        Vector3 frontScale = waveFront.transform.localScale;
        if (!isFacingRight) waveFront.transform.localScale = new Vector3(-frontScale.x, frontScale.y, frontScale.z);
        waveFront.GetComponent<ShockwaveController>()?.Setup(new Vector2(direction, 0f) * shockwaveSpeed);

        // 尻尾側から後ろ方向へ衝撃波
        GameObject waveBack = Instantiate(shockwavePrefab, tailPos, Quaternion.identity);
        // プレハブの元の大きさを維持したまま反転させるようにする
        Vector3 backScale = waveBack.transform.localScale;
        if (isFacingRight) waveBack.transform.localScale = new Vector3(-backScale.x, backScale.y, backScale.z);
        waveBack.GetComponent<ShockwaveController>()?.Setup(new Vector2(-direction, 0f) * shockwaveSpeed);
    }

    // 悪臭噴射のコルーチン
    IEnumerator GasAttackRoutine()
    {
        currentState = EnemyState.GasAttack;
        gasTimer = gasAttackCooldown; // クールダウンをリセット

        // 移動を止める
        if (moveCoroutine != null) StopCoroutine(moveCoroutine);
        
        // 少し潰れてガスを溜める構え
        transform.localScale = new Vector3(originalScale.x * 1.2f, originalScale.y * 0.7f, originalScale.z);
        
        // ガスを出す前に前後にブルブル振動させる
        Vector3 currentPos = transform.position;
        for (int i = 0; i < 5; i++)
        {
            transform.position = currentPos + new Vector3(0.08f, 0f, 0f);
            yield return new WaitForSeconds(0.04f);
            transform.position = currentPos + new Vector3(-0.08f, 0f, 0f);
            yield return new WaitForSeconds(0.04f);
        }
        transform.position = currentPos; // 位置をキッチリ元に戻す

        // 180度にランダムで大量のガスを飛ばす
        if (gasPrefab != null)
        {
            Vector3 gasPos = transform.position + new Vector3(0f, 1.0f, 0f); 
            
            // ばらまくガスの数
            int gasCount = 15;

            // ガスをばらまく瞬間にブワァーッという音を鳴らす
            if (gasSound != null && audioSource != null)
            {
                audioSource.PlayOneShot(gasSound);
            }

            for (int i = 0; i < gasCount; i++)
            {
                // 0度(右)から180度(左)の範囲でランダムな角度を決める
                float randomAngle = Random.Range(0f, 180f);
                
                // 三角関数を使って角度をXとYの向きのパワーに変換する
                float dirX = Mathf.Cos(randomAngle * Mathf.Deg2Rad);
                float dirY = Mathf.Sin(randomAngle * Mathf.Deg2Rad);

                // スピードも毎回ランダムにして、近くに落ちるものと遠くに飛ぶものを作る
                float randomSpeed = Random.Range(2.0f, 6.0f);
                Vector2 randomVelocity = new Vector2(dirX, dirY) * randomSpeed;

                GameObject gasObj = Instantiate(gasPrefab, gasPos, Quaternion.identity);
                
                // 衝撃波用ではなく、PoisonGasControllerを呼び出す
                gasObj.GetComponent<PoisonGasController>()?.Setup(randomVelocity);

                // ほんの少しだけ時間を空けて次を出すことで、ブワァーッと連続で噴き出している感を出す
                yield return new WaitForSeconds(0.02f);
            }
        }

        // 噴射している間は少し待機
        yield return new WaitForSeconds(gasDuration);

        // スケールを戻して通常の移動を再開
        transform.localScale = originalScale;
        moveCoroutine = StartCoroutine(MoveRoutine());
        currentState = EnemyState.Moving;
    }

    // 地面との接触判定
    private void OnCollisionStay2D(Collision2D collision)
    {
        // やられていたら当たり判定の処理をしない
        if (isDead) return;

        if (currentState == EnemyState.Jumping && collision.gameObject.CompareTag("Ground"))
        {
            // 上昇中ではなく、落下中か停止中であること
            if (rb.linearVelocity.y <= 0.1f)
            {
                // 壁ではなく「床（上向きの面）」に触れているか確認
                foreach (ContactPoint2D contact in collision.contacts)
                {
                    if (contact.normal.y > 0.5f) 
                    {
                        isGrounded = true;
                        break;
                    }
                }
            }
        }

        // プレイヤーが上に乗ったときの悪臭噴射カウンター
        if (currentState == EnemyState.Moving && collision.gameObject.CompareTag("Player"))
        {
            // プレイヤーのY座標が敵より高く、かつクールダウンが終わっているか
            if (collision.transform.position.y > transform.position.y + 0.5f && gasTimer <= 0f)
            {
                StartCoroutine(GasAttackRoutine());
            }
        }
    }

    // 壁のセンサーに触れたときのUターン処理、光線（レイキャスト）と合わせて二重にチェックする
    private void OnTriggerEnter2D(Collider2D other)
    {
        // やられていたら当たり判定の処理をしない
        if (isDead) return;

        // ジャンプ中は壁センサーでの強制Uターンを無効にする
        if (currentState == EnemyState.Jumping) return;

        if (other.CompareTag("Wall"))
        {
            isFacingRight = !isFacingRight;
            
            if (isFacingRight)
            {
                transform.localRotation = Quaternion.Euler(0, 180f, 0);
            }
            else
            {
                transform.localRotation = Quaternion.Euler(0, 0, 0);
            }

            // 進行方向が変わったので、移動ルーチンを一度リセットして向き直る
            if (currentState == EnemyState.Moving)
            {
                if (moveCoroutine != null) StopCoroutine(moveCoroutine);
                transform.localScale = originalScale;
                moveCoroutine = StartCoroutine(MoveRoutine());
            }
        }
    }
}
