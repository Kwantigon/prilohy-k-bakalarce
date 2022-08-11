#!/bin/bash
#PBS -N 5-lr
#PBS -q gpu
#PBS -l walltime=24:00:00
#PBS -l select=1:ncpus=8:ngpus=2:mem=16gb:scratch_local=32gb:cluster=glados
#PBS -m bae

marian_build_dir=/storage/brno3-cerit/home/kwan/marian-installation/marian/build-CUDA-10.1-CPU-sse2-ssse3-avx-avx2
this_run_dir=/storage/brno3-cerit/home/kwan/marian-training/final-runs/baseline/runs/baseline
marian_models_dir=$this_run_dir/models

log_file=$this_run_dir/logs/training-log.txt
train_set_cs=$this_run_dir/data/sentences-puncnormalized.tokenized.truecased.bpe.cs
train_set_vi=$this_run_dir/data/sentences-puncnormalized.tokenized.truecased.bpe.vi
vocabs_file=$this_run_dir/vocabs/vocab-36k.csvi.yml
valid_set_cs=~/marian-training/final-runs/baseline/testdata/testset/newstest2013-last1500sentences-puncnormalized.tokenized.truecased.bpe.cs
valid_set_vi=$this_run_dir/data/newstest2013-first1500sentences-puncnormalized.tokenized.truecased.bpe.vi

# Add cuda to run marian training.
module add cuda-10.1

# Create a common vocabulary.
if [ ! -e "$vocabs_file" ]; then
	cat "$train_set_cs" "$train_set_vi" | $marian_build_dir/marian-vocab --max-size 36000 > "$vocabs_file"
fi

# The option --sync-sgd is recommended for the transformer model in marian documentation.
$marian_build_dir/marian \
	--devices 0 1 \
	--model $marian_models_dir/model.npz --type transformer \
	--sync-sgd \
	--train-sets "$train_set_cs" "$train_set_vi" \
	--max-length 50 --mini-batch-fit --maxi-batch 1000 --workspace 6000 \
	--vocabs "$vocabs_file" "$vocabs_file" \
	--valid-sets "$valid_set_cs" "$valid_set_vi" \
	--valid-metrics bleu --valid-freq 5000 --valid-mini-batch 64 \
	--valid-translation-output "$this_run_dir/validation/valid-{E}.out" \
	--valid-log "$this_run_dir/validation/valid-log.txt" \
	--save-freq 5000 --disp-freq 5000 \
	--log "$log_file" \
	--beam-size 6 \
	--enc-depth 6 --dec-depth 6 \
	--transformer-heads 8 \
	--transformer-postprocess-emb d \
	--transformer-postprocess dan \
	--transformer-dropout 0.1 --label-smoothing 0.1 --exponential-smoothing \
	--learn-rate 0.0005 --lr-warmup 16000 --lr-decay-inv-sqrt 16000 --lr-report \
	--optimizer-params 0.9 0.98 1e-09 --clip-norm 5 \
	--tied-embeddings-all \
	--seed 690
