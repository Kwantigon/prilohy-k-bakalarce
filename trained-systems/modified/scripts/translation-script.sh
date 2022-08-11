#!/bin/bash
#PBS -N modified-translation
#PBS -q gpu
#PBS -l walltime=1:00:00
#PBS -l select=1:ncpus=8:ngpus=1:mem=16gb:scratch_local=32gb:cluster=glados

marian_build_dir=/storage/brno3-cerit/home/kwan/marian-installation/marian/build-CUDA-10.1-CPU-sse2-ssse3-avx-avx2
this_run_dir=/storage/brno3-cerit/home/kwan/marian-training/final-runs/modified
marian_models_dir=$this_run_dir/models/marian
cs_input=$this_run_dir/normal-text/newstest2013-last1500sentences-puncnormalized.tokenized.truecased.bpe.cs
vi_output=$this_run_dir/newstest2013-translated-last1500sentences-puncnormalized.tokenized.truecased.bpe.vi
vi_reference=$this_run_dir/normal-text/newstest2013-last1500sentences-puncnormalized.tokenized.truecased.bpe.vi
bleu="~/mosesdecoder/scripts/generic/multi-bleu.perl"

module add cuda-10.1

$marian_build_dir/marian-decoder \
	-d 0 \
	-c "${marian_models_dir}/model.npz.decoder.yml" \
	-m "${marian_models_dir}/model.iter35000.npz" \
	< "$cs_input" \
	> "$vi_output"

#"$bleu" "$vi_reference" < "$vi_output" > "$this_run_dir/output-translation/score.txt"
