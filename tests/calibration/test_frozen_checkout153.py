"""Real Git smudge tests: preserve the sealed bytes with either Windows autocrlf setting."""
import hashlib,json,subprocess,tempfile,unittest
from pathlib import Path
R=Path(__file__).resolve().parents[2]
BASE='a9024b5bb141e8158fc7c441705768a7b7354d35'
class FrozenCheckout153Tests(unittest.TestCase):
 def test_frozen_blobs_remain_identical_to_exact_pr151_head(self):
  manifest=json.loads((R/'docs/experiments/efficient-contingency/source-proof.json').read_bytes())
  proof=json.loads((R/'docs/experiments/safe-fast-contingency/checkout-audit.json').read_bytes())
  self.assertEqual(proof['Base'],BASE)
  # The Python job deliberately uses a shallow checkout. Keep exact base blob
  # identities in the new audit instead of requiring unavailable ancestor objects.
  raw=subprocess.check_output(['git','ls-tree','-r','HEAD'],cwd=R,text=True)
  objects={line.split('\t',1)[1]:line.split('\t',1)[0].split()[2] for line in raw.splitlines()}
  self.assertEqual({p:objects[p] for p in manifest['FrozenHashes']},proof['FrozenGitBlobs'])
 def test_actual_git_checkout_reproduces_every_sealed_hash_in_both_modes(self):
  hashes=json.loads((R/'docs/experiments/efficient-contingency/source-proof.json').read_bytes())['FrozenHashes']
  for mode in ['false','true']:
   with self.subTest(autocrlf=mode),tempfile.TemporaryDirectory(prefix='checkout153-') as folder:
    prefix=Path(folder).as_posix()+'/'
    subprocess.run(['git','-c','core.autocrlf='+mode,'checkout-index','--force','--prefix='+prefix,'-z','--stdin'],input=('\0'.join(hashes)+'\0').encode(),cwd=R,check=True,capture_output=True)
    for path,expected in hashes.items():
     self.assertEqual(hashlib.sha256((Path(folder)/path).read_bytes()).hexdigest(),expected,(mode,path))
if __name__=='__main__':unittest.main()
