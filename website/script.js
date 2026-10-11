// TranslatePet 网站交互脚本
document.addEventListener('DOMContentLoaded', () => {
  // 1. 哆啦A梦桌宠帧动画播放器
  initPetPlayer();

  // 2. 地道口语对比模拟器
  initConversationalDemo();

  // 3. 艾宾浩斯生词卡片翻牌模拟
  initFlashcardDemo();

  // 4. 专属提示词选项卡
  initPromptTabs();

  // 5. 代码块一键复制
  initCodeCopyButtons();

  // 6. 导航栏滚动高亮
  initNavScroll();
});

/* ================= 1. 桌宠帧动画播放器 ================= */
function initPetPlayer() {
  const petImg = document.getElementById('petFrameImg');
  const petBubble = document.getElementById('petBubbleText');
  const playPauseBtn = document.getElementById('btnPlayPausePet');
  const eatBtn = document.getElementById('btnEatDorayaki');
  if (!petImg) return;

  const totalFrames = 8;
  let currentFrame = 0;
  let isPlaying = true;
  let frameInterval = 120; // ms
  let timer = null;

  const petQuotes = [
    "大雄！今天背单词了吗？",
    "按 F4 随时呼唤我截图翻译哦！",
    "铜锣烧真好吃！😋",
    "和外国友人聊天，要用地道口语哦！",
    "艾宾浩斯曲线正在保护你的记忆力！",
    "在设置里随时定制专属提示词哦！"
  ];

  function updateFrame() {
    petImg.src = `assets/frame_${currentFrame}.png`;
    currentFrame = (currentFrame + 1) % totalFrames;
  }

  function startAnimation() {
    if (timer) clearInterval(timer);
    timer = setInterval(updateFrame, frameInterval);
    isPlaying = true;
    if (playPauseBtn) playPauseBtn.textContent = '⏸️ 暂停动画';
  }

  function pauseAnimation() {
    if (timer) clearInterval(timer);
    timer = null;
    isPlaying = false;
    if (playPauseBtn) playPauseBtn.textContent = '▶️ 播放动画';
  }

  startAnimation();

  if (playPauseBtn) {
    playPauseBtn.addEventListener('click', () => {
      if (isPlaying) pauseAnimation();
      else startAnimation();
    });
  }

  // 点击桌宠或互动气泡
  const petScreen = document.getElementById('petScreen');
  if (petScreen) {
    petScreen.addEventListener('click', () => {
      const randomQuote = petQuotes[Math.floor(Math.random() * petQuotes.length)];
      if (petBubble) {
        petBubble.textContent = randomQuote;
        petBubble.style.animation = 'none';
        void petBubble.offsetWidth; // trigger reflow
        petBubble.style.animation = 'floatBubble 3s ease-in-out infinite';
      }
    });
  }

  const peekBtn = document.getElementById('btnPeekEdge');

  if (eatBtn) {
    eatBtn.addEventListener('click', () => {
      if (petBubble) petBubble.textContent = "美味的铜锣烧！(嚼嚼嚼...) 🥞";
      frameInterval = 70; // 快速吃
      startAnimation();
      setTimeout(() => {
        frameInterval = 120;
        startAnimation();
      }, 2500);
    });
  }

  if (peekBtn) {
    let isPeeking = false;
    let peekTimer = null;
    let peekIdx = 0;

    peekBtn.addEventListener('click', () => {
      isPeeking = !isPeeking;
      if (isPeeking) {
        pauseAnimation();
        peekBtn.textContent = '🔄 恢复常态待机';
        if (petBubble) petBubble.textContent = "我在屏幕边缘探头看着你哦！👀";

        peekIdx = 0;
        petImg.src = `assets/frame_peek_right_0.png`;
        peekTimer = setInterval(() => {
          peekIdx = (peekIdx + 1) % 4;
          petImg.src = `assets/frame_peek_right_${peekIdx}.png`;
        }, 320);
      } else {
        if (peekTimer) clearInterval(peekTimer);
        peekBtn.textContent = '🧲 模拟边缘探头';
        if (petBubble) petBubble.textContent = "大雄，今天背单词了吗？🐱";
        startAnimation();
      }
    });
  }
}

/* ================= 2. 地道口语对比模拟器 ================= */
const conversationalData = {
  down: {
    source: "Are you coming tonight? — I'm down for that!",
    literal: "你今晚来吗？—— 我为此而向下！",
    literalComment: "字面直译不知所云，误将 down 理解为向下方位。",
    natural: "今晚你来吗？—— 算我一个，我肯定去！",
    tips: "💡 社交口语解析：'be down for something' 在北美日常口语中表示'完全赞同、极乐意加入'。地道替换词还有：'Count me in!' 或 'I'm in!'"
  },
  break: {
    source: "Break a leg at your presentation today!",
    literal: "祝你今天的演讲断一条腿！",
    literalComment: "惊恐式直译，容易让外国朋友误以为你是在诅咒。",
    natural: "祝你今天的演讲顺顺利利，旗开得胜！",
    tips: "💡 戏剧与社交俚语：源自欧美剧场文化，认为直接说'祝好运'会带来厄运，因此反语说'Break a leg'表示'大获成功、演出精彩'。"
  },
  touchbase: {
    source: "Let's touch base first thing tomorrow morning.",
    literal: "明天一早让我们去摸一下垒包。",
    literalComment: "生搬硬套棒球术语，完全脱离职场商务沟通语境。",
    natural: "明天一早咱们同步对一下进展 / 碰个头。",
    tips: "💡 外企高频口语：'touch base' 原出自棒球击球手回垒，现广泛用于日常办公中'简短沟通交流、对齐一下信息'。"
  },
  weather: {
    source: "I'm feeling a bit under the weather today.",
    literal: "我今天感觉在天气下面。",
    literalComment: "无意义字面翻译，未能传达身体不适的隐喻。",
    natural: "我今天身体有点不太得劲 / 感觉微恙。",
    tips: "💡 地道隐喻表达：源自古代航海，遇恶劣风浪天气时水手会躲到甲板下方，后引申为'身体稍有不适但无大碍'。"
  },
  onme: {
    source: "Put your wallet away, lunch is on me today.",
    literal: "收起钱包，今天的午饭都在我身上。",
    literalComment: "容易引起歧义或滑稽感，直译生硬。",
    natural: "把钱包收起来，今天的午餐我包了 / 我买单！",
    tips: "💡 请客必备表达：'It's on me' 是朋友聚餐抢单最自然地道的方式。同义地道表达还有：'My treat!' 或 'I got this!'"
  }
};

function initConversationalDemo() {
  const pills = document.querySelectorAll('.demo-pills .pill-btn');
  const sourceEl = document.getElementById('demoSourceText');
  const literalEl = document.getElementById('demoLiteralResult');
  const literalCommEl = document.getElementById('demoLiteralComment');
  const naturalEl = document.getElementById('demoNaturalResult');
  const tipEl = document.getElementById('demoTipText');

  if (!pills.length) return;

  pills.forEach(pill => {
    pill.addEventListener('click', () => {
      pills.forEach(p => p.classList.remove('active'));
      pill.classList.add('active');

      const key = pill.getAttribute('data-demo');
      const item = conversationalData[key];
      if (!item) return;

      if (sourceEl) sourceEl.textContent = item.source;
      if (literalEl) literalEl.textContent = item.literal;
      if (literalCommEl) literalCommEl.textContent = item.literalComment;
      if (naturalEl) naturalEl.textContent = item.natural;
      if (tipEl) tipEl.textContent = item.tips;
    });
  });
}

/* ================= 3. 艾宾浩斯生词卡片翻牌模拟 ================= */
const vocabCardsData = [
  {
    word: "serendipity",
    phonetic: "/ˌserənˈdɪpəti/",
    example: "Meeting you here was pure serendipity. (在这里遇见你真是奇妙的意外之喜。)",
    definition: "n. 机缘巧合；意外发现珍宝的运气与福气",
    hack: "💡 词源记忆法：源自古波斯童话《锡兰三王子》(The Three Princes of Serendip)，主人公们总是在漫游中意外发现美好的东西。记忆：Seren (平静) + dip (探寻) = 意外邂逅的惊喜！",
    stage: "第 1 阶段（初始录入）"
  },
  {
    word: "down-to-earth",
    phonetic: "/ˌdaʊn tu ˈɜːrθ/",
    example: "Despite his massive success, he remains very down-to-earth. (尽管大获成功，他依然十分务实接地气。)",
    definition: "adj. 脚踏实地的；谦逊务实的；接地气的",
    hack: "💡 联想记忆法：脚踩在大地 (earth) 之上，不飘在空中 = 极度脚踏实地、随和平易近人。",
    stage: "第 3 阶段（已记住 4 天）"
  },
  {
    word: "procrastinate",
    phonetic: "/prəˈkræstɪneɪt/",
    example: "Stop procrastinating and finish your assignment now! (别再拖延了，现在就把作业写完！)",
    definition: "v. 拖延；耽搁；拖延症",
    hack: "💡 词根拆解：pro- (向前、推迟) + cras (拉丁语'明天') + -tinate => 把所有事情都推到明天做 = 拖延！",
    stage: "第 5 阶段（已掌握 15 天）"
  }
];

let currentCardIndex = 0;

function initFlashcardDemo() {
  const flashcard = document.getElementById('demoFlashcard');
  const flipBtn = document.getElementById('btnFlipCard');
  const wordEl = document.getElementById('cardWord');
  const phoneticEl = document.getElementById('cardPhonetic');
  const exampleEl = document.getElementById('cardExample');
  const defEl = document.getElementById('cardDef');
  const hackEl = document.getElementById('cardHack');
  const stageEl = document.getElementById('cardStage');

  function renderCard(idx) {
    const data = vocabCardsData[idx];
    if (wordEl) wordEl.textContent = data.word;
    if (phoneticEl) phoneticEl.textContent = data.phonetic;
    if (exampleEl) exampleEl.textContent = data.example;
    if (defEl) defEl.textContent = data.definition;
    if (hackEl) hackEl.textContent = data.hack;
    if (stageEl) stageEl.textContent = data.stage;
  }

  function toggleFlip() {
    if (flashcard) flashcard.classList.toggle('flipped');
  }

  if (flashcard) {
    flashcard.addEventListener('click', (e) => {
      // 避免点击内部交互按钮时再次翻转
      if (e.target.closest('.card-btn-action')) return;
      toggleFlip();
    });
  }

  if (flipBtn) {
    flipBtn.addEventListener('click', (e) => {
      e.stopPropagation();
      toggleFlip();
    });
  }

  // 记忆反馈按钮
  const feedbackBtns = document.querySelectorAll('.card-feedback-btn');
  feedbackBtns.forEach(btn => {
    btn.addEventListener('click', (e) => {
      e.stopPropagation();
      const feedbackType = btn.getAttribute('data-feedback');
      const feedbackMsg = feedbackType === 'easy' ? '已标记为完全掌握，下一次复习安排在 7 天后！' :
                         feedbackType === 'good' ? '已标记为有些模糊，将在 2 天后再次重温。' :
                         '已标记为遗忘，系统已安排在 12 小时内强化巩固！';

      alert(`[艾宾浩斯调度反馈]\n${feedbackMsg}`);

      // 切换到下一个示例词
      currentCardIndex = (currentCardIndex + 1) % vocabCardsData.length;
      if (flashcard && flashcard.classList.contains('flipped')) {
        flashcard.classList.remove('flipped');
        setTimeout(() => renderCard(currentCardIndex), 300);
      } else {
        renderCard(currentCardIndex);
      }
    });
  });

  renderCard(0);
}

/* ================= 4. 专属提示词选项卡 ================= */
function initPromptTabs() {
  const tabs = document.querySelectorAll('.tab-btn');
  const contents = document.querySelectorAll('.prompt-content-view');

  tabs.forEach(tab => {
    tab.addEventListener('click', () => {
      tabs.forEach(t => t.classList.remove('active'));
      contents.forEach(c => c.classList.remove('active'));

      tab.classList.add('active');
      const targetId = tab.getAttribute('data-tab');
      const targetContent = document.getElementById(targetId);
      if (targetContent) targetContent.classList.add('active');
    });
  });
}

/* ================= 5. 代码块一键复制 ================= */
function initCodeCopyButtons() {
  const copyBtns = document.querySelectorAll('.copy-code-btn');
  copyBtns.forEach(btn => {
    btn.addEventListener('click', () => {
      const parent = btn.closest('.code-box') || btn.closest('.prompt-content-view');
      const codeElement = parent.querySelector('code') || parent.querySelector('pre');
      const text = codeElement ? codeElement.innerText : '';

      navigator.clipboard.writeText(text).then(() => {
        const originalText = btn.textContent;
        btn.textContent = '✅ 已复制!';
        setTimeout(() => {
          btn.textContent = originalText;
        }, 2000);
      }).catch(err => {
        console.error('Copy failed:', err);
      });
    });
  });
}

/* ================= 6. 导航栏滚动高亮 ================= */
function initNavScroll() {
  const sections = document.querySelectorAll('section[id]');
  const navLinks = document.querySelectorAll('.nav-links a');

  window.addEventListener('scroll', () => {
    let currentId = '';
    const scrollPos = window.scrollY + 200;

    sections.forEach(sec => {
      const top = sec.offsetTop;
      const height = sec.offsetHeight;
      if (scrollPos >= top && scrollPos < top + height) {
        currentId = sec.getAttribute('id');
      }
    });

    navLinks.forEach(link => {
      link.classList.remove('active');
      if (link.getAttribute('href') === `#${currentId}`) {
        link.classList.add('active');
      }
    });
  });
}
