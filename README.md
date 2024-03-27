### 高性能的开源Era字典工具
我也不想造轮子啊！我也想用现成的轮子改一改就用啊！  
但是找不到，找不到这种一键提取所有era文本并替换的开源轮子，于是我上了。  
没有做编辑界面是因为根本不需要。导出的字典可以直接上传[Paratranz](https://paratranz.cn/)。  
Paratranz是一个操作简单功能强大的多人协作平台。只要设备能访问网页，就能参与汉化。  
毫不夸张地说，体验了如此现代化的Paratranz之后，就很难再接受其它翻译工作流了。  
说回字典工具，因为我才刚接触EraBasic，可能会有许多提取和替换上的错漏，希望大家能帮我测试修正，提供建议和反馈！  
目前我使用EraOCG2作为测试样本，表现良好，提取和汉化分别只需要1秒和2秒，暂未发现错漏之处。需要更多更大的测试样本！  
有什么需求也欢迎提出，我会加入~~人才库~~需求池的！  
已经在需求池的也可以提，我想知道是否真的有对应的需求。  
### 目前支持
✅ **提取文本**：从游戏目录提取未翻译的字典；  
✅ **导入译文**：使用已翻译的字典汉化游戏；  
✅ **快速迁移**：对照原版游戏和已汉化游戏，提取出已翻译的字典。无论处于什么汉化阶段，都能使用这个功能快速迁移工作流；  
✅ **版本更新**：从新版本游戏中提取增删的条目，插入到原字典中，兼顾上下文顺序的同时，维持原本的Key值；  
✅ **开放配置**：用户可以在config.json下修改设置。当然直接改代码也很方便，几乎三五行代码就有一行注释；  
### TODO
- [x] **变量修正**：扫描已翻译字典，找到翻译不一致的变量，让译者快速修正；
- [x] **想个名字**：不！这个工具甚至还没有一个名字！
### 需求池
- [x] **死亡笔记**：在配置文件里配置的文件和词条不会被提取和翻译；
- [x] **快速填充**：遍历未翻译的条目，然后查找相同的已翻译的条目进行填充；
- [x] **字典转换**：将此工具的字典转换成其他工具的字典，或者反过来；
- [x] **安全更新**：版本更新的时候，过时的词条不会删除，而是隐藏（Paratranz已有类似功能）；
- [x] **机器翻译**：配置Token来批量机翻（Paratranz已有类似功能）；
- [x] **用户界面**：我打Paratranz？诶……真的假的……；
### 懒得做的
- [x] **ERB行末注释**：对，我处理了CSV行末注释，但没处理ERB的。虽然做起来不麻烦，但就是不高兴做；
- [x] **花括号合并多行代码**：似乎没有游戏使用，就当这个特性不存在吧，等到真有需求了再做也是举手之劳；
- [x] **特別なコメント行**：暂不支持“;!;”和“;#;”；
- [x] **同目录下有同名ERB和ERH会导致键值冲突**：早期设计失误，取了短文件名做键值。懒得补救了，摆了，真出事了再修；
