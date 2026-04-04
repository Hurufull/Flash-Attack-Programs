using UnityEngine;
using TMPro; // TextMeshProを使うために追加

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{
    [Header("UIテキスト設定")]
    public TextMeshProUGUI hpText; // 新規追加: HPの文字表示用
    public TextMeshProUGUI mpText; // 新規追加: MPの文字表示用

    [Header("ステータス設定")]
    public float maxHP = 100f;
    public float currentHP;
    public float maxMP = 100f;
    public float currentMP;
    public float mpRecoveryRate = 10f; // MPは自動で回復する

    [Header("移動・ジャンプ設定")]
    public float moveSpeed = 10f;
    public float jumpForce = 12f;
    private Rigidbody2D rb;
    private bool isGrounded;

    [Header("ローリング設定")]
    public float rollSpeed = 15f;
    public float rollDuration = 0.5f;
    public float rollCooldown = 1f;
    private bool isRolling = false;
    private float rollCooldownTimer = 0f;

    [Header("攻撃設定")]
    public float baseAttackPower = 20f;
    public float comboMultiplier = 1f; // 攻撃をし続けると与えるダメージが上昇していく
    public float comboResetTime = 2.5f; // 一定時間たつとリセットされてしまう
    private int comboCount = 0;
    private float comboTimer = 0f;

    [Header("魔法・アイテム設定")]
    public float healAmount = 10f;
    public float healMpCost = 30f;
    public GameObject magicAttackPrefab; // 魔法攻撃のプレハブ
    public float magicMpCost = 35f;
    public GameObject poisonBottlePrefab; // 毒ビンのプレハブ

    // 状態管理
    // 新規追加: 無敵状態かどうかを判定する変数
    public bool isInvincible = false;
    // 新規追加: やられている状態かどうかを判定する変数
    public bool isDead = false;
    public float totalDamageTaken = 0f; // これまでに受けた合計ダメージ

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        currentHP = maxHP;
        currentMP = maxMP;
    }

    void Update()
    {
        // 新規追加: もしやられていたら、これ以降の操作の処理を一切行わずにここで終わる
        if (isDead) return;

        // 新規追加: 常にHPとMPの文字を最新の状態に更新する
        UpdateStatusText();

        // MPの自動回復処理
        RecoverMP();

        // コンボ継続のタイマー処理
        UpdateComboTimer();

        // ローリングのクールダウン処理
        if (rollCooldownTimer > 0) rollCooldownTimer -= Time.deltaTime;

        // プレイヤーの各種入力処理
        HandleMovement();
        HandleJump();
        HandleRoll();
        HandleAttack();
        HandleMagic();
        HandleItem();
    }

    void RecoverMP()
    {
        // MPは自動で回復する
        if (currentMP < maxMP)
        {
            currentMP += mpRecoveryRate * Time.deltaTime;
            if (currentMP > maxMP) currentMP = maxMP;
        }
    }

    void UpdateComboTimer()
    {
        if (comboCount > 0)
        {
            comboTimer -= Time.deltaTime;
            if (comboTimer <= 0)
            {
                // 一定時間たつとリセットされてしまう
                comboCount = 0;
            }
        }
    }

    void HandleMovement()
    {
        if (isRolling) return; // ローリング中は方向転換できない

        float moveInput = Input.GetAxisRaw("Horizontal");
        rb.linearVelocity = new Vector2(moveInput * moveSpeed, rb.linearVelocity.y);

        // キャラクターの向きを反転させる
        if (moveInput > 0) transform.localScale = new Vector3(1, 1, 1);
        else if (moveInput < 0) transform.localScale = new Vector3(-1, 1, 1);
    }

    void HandleJump()
    {
        if (isRolling) return;

        if (Input.GetButtonDown("Jump") && isGrounded)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
            isGrounded = false;
        }
    }

    void HandleRoll()
    {
        // スペースキーでローリング
        if (Input.GetKeyDown(KeyCode.LeftShift) && rollCooldownTimer <= 0 && !isRolling && isGrounded)
        {
            StartCoroutine(RollRoutine());
        }
    }

    System.Collections.IEnumerator RollRoutine()
    {
        isRolling = true;
        isInvincible = true; // ローリング中は無敵時間になる
        rollCooldownTimer = rollCooldown;

        float direction = transform.localScale.x;
        rb.linearVelocity = new Vector2(direction * rollSpeed, rb.linearVelocity.y);

        yield return new WaitForSeconds(rollDuration);

        isRolling = false;
        isInvincible = false; // 無敵時間を終了する
    }

    void HandleAttack()
    {
        // Jキーで通常の武器攻撃
        if (Input.GetKeyDown(KeyCode.J))
        {
            PerformAttack();
        }
    }

    void PerformAttack()
    {
        // 攻撃をし続けるとダメージが上昇する仕組み
        comboCount++;
        comboTimer = comboResetTime; // コンボの猶予時間をリセット

        // 現在のコンボ数に応じて攻撃力を計算する
        float currentAttackPower = baseAttackPower * Mathf.Pow(comboMultiplier, comboCount - 1);
        
        Debug.Log("武器攻撃！ ダメージ: " + currentAttackPower + " 現在のコンボ数: " + comboCount);
    }

    void HandleMagic()
    {
        // CキーでHPの回復魔法
        if (Input.GetKeyDown(KeyCode.C))
        {
            if (currentMP >= healMpCost && currentHP < maxHP)
            {
                currentMP -= healMpCost;
                currentHP += healAmount;
                if (currentHP > maxHP) currentHP = maxHP;
                Debug.Log("HP回復魔法を使用しました");
            }
        }

        // Kキーで魔法攻撃
        if (Input.GetKeyDown(KeyCode.K))
        {
            if (currentMP >= magicMpCost)
            {
                currentMP -= magicMpCost;
                if (magicAttackPrefab != null)
                {
                    Instantiate(magicAttackPrefab, transform.position + new Vector3(transform.localScale.x, 0, 0), Quaternion.identity);
                }
            }
        }
    }

    void HandleItem()
    {
        // Lキーで毒ビンを投げる
        // 毒ビンを投げるキーを押している間でも、武器や魔法で攻撃できる
        if (Input.GetKeyDown(KeyCode.L))
        {
             if (poisonBottlePrefab != null)
             {
                 Instantiate(poisonBottlePrefab, transform.position + new Vector3(transform.localScale.x, 0.5f, 0), Quaternion.identity);
             }
        }
    }

    public void TakeDamage(float damage)
    {
        // 無敵状態ならダメージを受けずに処理を終わる
        if (isInvincible) return;

        // 受けたダメージを合計ダメージの変数にどんどん足して記憶させます
        totalDamageTaken += damage;
        
        currentHP -= damage;
        
        // HPが0以下になった時の処理
        if (currentHP <= 0)
        {
            // やられた状態をオンにして、操作を受け付けなくする
            isDead = true;

            // 慣性で滑っていかないように、プレイヤーの物理的な動きを完全にピタッと止める
            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
            }

            // ゲームオーバー画面を表示するように命令を出す
            GameOverManager gameOverManager = Object.FindFirstObjectByType<GameOverManager>();
            if (gameOverManager != null)
            {
                gameOverManager.ShowGameOver();
            }
        }
    }

    // 新規追加: HPとMPの文字表示を更新するメソッド
    public void UpdateStatusText()
    {
        if (hpText != null)
        {
            // マイナス表示にならないように0で止めつつ、整数にして表示
            int displayHP = Mathf.Max(0, Mathf.FloorToInt(currentHP));
            int displayMaxHP = Mathf.FloorToInt(maxHP);
            hpText.text = "HP:" + displayHP.ToString() + "/" + displayMaxHP.ToString();
        }

        if (mpText != null)
        {
            // MPも同じように表示
            int displayMP = Mathf.Max(0, Mathf.FloorToInt(currentMP));
            int displayMaxMP = Mathf.FloorToInt(maxMP);
            mpText.text = "MP:" + displayMP.ToString() + "/" + displayMaxMP.ToString();
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        // 足場に着地したかどうかの判定
        if (collision.gameObject.CompareTag("Ground"))
        {
            isGrounded = true;
        }
    }
}
